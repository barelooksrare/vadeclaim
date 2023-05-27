using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solnet;
using Solnet.Programs.Abstract;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Core.Sockets;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Vadeclaim;
using Vadeclaim.Program;
using Vadeclaim.Errors;
using Vadeclaim.Accounts;
using Vadeclaim.Types;

namespace Vadeclaim
{
    namespace Accounts
    {
        public partial class Counter
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 1836621736066724095UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{255, 176, 4, 245, 188, 253, 124, 25};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "jmVQbGxuVYt";
            public bool[] AnimalsCollected { get; set; }

            public bool[] PlantsCollected { get; set; }

            public bool[] MushroomsCollected { get; set; }

            public bool[] ArtifactsCollected { get; set; }

            public long StartedAt { get; set; }

            public static Counter Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Counter result = new Counter();
                result.AnimalsCollected = new bool[14];
                for (uint resultAnimalsCollectedIdx = 0; resultAnimalsCollectedIdx < 14; resultAnimalsCollectedIdx++)
                {
                    result.AnimalsCollected[resultAnimalsCollectedIdx] = _data.GetBool(offset);
                    offset += 1;
                }

                result.PlantsCollected = new bool[12];
                for (uint resultPlantsCollectedIdx = 0; resultPlantsCollectedIdx < 12; resultPlantsCollectedIdx++)
                {
                    result.PlantsCollected[resultPlantsCollectedIdx] = _data.GetBool(offset);
                    offset += 1;
                }

                result.MushroomsCollected = new bool[8];
                for (uint resultMushroomsCollectedIdx = 0; resultMushroomsCollectedIdx < 8; resultMushroomsCollectedIdx++)
                {
                    result.MushroomsCollected[resultMushroomsCollectedIdx] = _data.GetBool(offset);
                    offset += 1;
                }

                result.ArtifactsCollected = new bool[8];
                for (uint resultArtifactsCollectedIdx = 0; resultArtifactsCollectedIdx < 8; resultArtifactsCollectedIdx++)
                {
                    result.ArtifactsCollected[resultArtifactsCollectedIdx] = _data.GetBool(offset);
                    offset += 1;
                }

                result.StartedAt = _data.GetS64(offset);
                offset += 8;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum VadeclaimErrorKind : uint
        {
            ItemIdAlreadyCollected = 6000U,
            ItemNotYetCollected = 6001U,
            SetNotComplete = 6002U,
            TooManyMints = 6003U,
            TooFewMints = 6004U,
            NotDepositedMint = 6005U
        }
    }

    namespace Types
    {
    }

    public partial class VadeclaimClient : TransactionalBaseClient<VadeclaimErrorKind>
    {
        public VadeclaimClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solnet.Programs.Models.ProgramAccountsResultWrapper<List<Counter>>> GetCountersAsync(string programAddress, Commitment commitment = Commitment.Confirmed)
        {
            var list = new List<Solnet.Rpc.Models.MemCmp>{new Solnet.Rpc.Models.MemCmp{Bytes = Counter.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solnet.Programs.Models.ProgramAccountsResultWrapper<List<Counter>>(res);
            List<Counter> resultingAccounts = new List<Counter>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Counter.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solnet.Programs.Models.ProgramAccountsResultWrapper<List<Counter>>(res, resultingAccounts);
        }

        public async Task<Solnet.Programs.Models.AccountResultWrapper<Counter>> GetCounterAsync(string accountAddress, Commitment commitment = Commitment.Confirmed)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solnet.Programs.Models.AccountResultWrapper<Counter>(res);
            var resultingAccount = Counter.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solnet.Programs.Models.AccountResultWrapper<Counter>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeCounterAsync(string accountAddress, Action<SubscriptionState, Solnet.Rpc.Messages.ResponseValue<Solnet.Rpc.Models.AccountInfo>, Counter> callback, Commitment commitment = Commitment.Confirmed)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Counter parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Counter.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendDepositAsync(DepositAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solnet.Rpc.Models.TransactionInstruction instr = Program.VadeclaimProgram.Deposit(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendWithdrawAsync(WithdrawAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solnet.Rpc.Models.TransactionInstruction instr = Program.VadeclaimProgram.Withdraw(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendClaimRewardAsync(ClaimRewardAccounts accounts, byte[] bumps, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solnet.Rpc.Models.TransactionInstruction instr = Program.VadeclaimProgram.ClaimReward(accounts, bumps, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<VadeclaimErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<VadeclaimErrorKind>>{{6000U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.ItemIdAlreadyCollected, "Item Id already collected")}, {6001U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.ItemNotYetCollected, "Item Id not yet collected")}, {6002U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.SetNotComplete, "Can't claim without all the pieces, sorry.")}, {6003U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.TooManyMints, "Too many mints ser")}, {6004U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.TooFewMints, "Not enough mints ser")}, {6005U, new ProgramError<VadeclaimErrorKind>(VadeclaimErrorKind.NotDepositedMint, "Stop injecting random mints")}, };
        }
    }

    namespace Program
    {
        public class DepositAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey Counter { get; set; }

            public PublicKey CategoryAuth { get; set; }

            public PublicKey MetadataAccount { get; set; }

            public PublicKey SourceAccount { get; set; }

            public PublicKey DestinationAccount { get; set; }

            public PublicKey Mint { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class WithdrawAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey Counter { get; set; }

            public PublicKey CategoryAuth { get; set; }

            public PublicKey MetadataAccount { get; set; }

            public PublicKey SourceAccount { get; set; }

            public PublicKey DestinationAccount { get; set; }

            public PublicKey Mint { get; set; }

            public PublicKey AssociatedTokenProgram { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class ClaimRewardAccounts
        {
            public PublicKey Signer { get; set; }

            public PublicKey Counter { get; set; }

            public PublicKey DestinationAccount { get; set; }

            public PublicKey RewardMint { get; set; }

            public PublicKey SourceAccount { get; set; }

            public PublicKey CategoryAuth { get; set; }

            public PublicKey ProgramAuth { get; set; }

            public PublicKey AssociatedTokenProgram { get; set; }

            public PublicKey TokenProgram { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public static class VadeclaimProgram
        {
            public static Solnet.Rpc.Models.TransactionInstruction Deposit(DepositAccounts accounts, PublicKey programId)
            {
                List<Solnet.Rpc.Models.AccountMeta> keys = new()
                {Solnet.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solnet.Rpc.Models.AccountMeta.Writable(accounts.Counter, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.CategoryAuth, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.MetadataAccount, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.SourceAccount, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.DestinationAccount, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.Mint, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(13182846803881894898UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solnet.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solnet.Rpc.Models.TransactionInstruction Withdraw(WithdrawAccounts accounts, PublicKey programId)
            {
                List<Solnet.Rpc.Models.AccountMeta> keys = new()
                {Solnet.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solnet.Rpc.Models.AccountMeta.Writable(accounts.Counter, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.CategoryAuth, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.MetadataAccount, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.SourceAccount, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.DestinationAccount, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.Mint, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(2495396153584390839UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solnet.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solnet.Rpc.Models.TransactionInstruction ClaimReward(ClaimRewardAccounts accounts, byte[] bumps, PublicKey programId)
            {
                List<Solnet.Rpc.Models.AccountMeta> keys = new()
                {Solnet.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solnet.Rpc.Models.AccountMeta.Writable(accounts.Counter, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.DestinationAccount, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardMint, false), Solnet.Rpc.Models.AccountMeta.Writable(accounts.SourceAccount, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.CategoryAuth, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.ProgramAuth, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false), Solnet.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(11717902644310007701UL, offset);
                offset += 8;
                _data.WriteS32(bumps.Length, offset);
                offset += 4;
                _data.WriteSpan(bumps, offset);
                offset += bumps.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solnet.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}