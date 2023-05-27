using Newtonsoft.Json;
using Solnet.Rpc;
using Solnet.Wallet;
using Vadeclaim;
using Vadeclaim.Utils;
using System.Text;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Types;
using Solnet.Rpc.Models;

namespace vadelib.test;

[TestClass]
public class UnitTest1
{
    private readonly Dictionary<string, string> env;
    private readonly IRpcClient rpc;
    private readonly IStreamingRpcClient ws;
    private readonly VadeclaimClient client;

    private readonly PublicKey[] mints;

    public UnitTest1()
    {
        env = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("./.env"))!;
        Console.WriteLine(env["rpc"]);
        rpc = ClientFactory.GetClient(env["rpc"], null, null);
        ws = ClientFactory.GetStreamingClient(env["ws"], null, null);
        client = new VadeclaimClient(rpc, null, new PublicKey(env["programId"]));
        var key = new{pubkey = string.Empty};
        mints = Directory.GetFiles("/Users/bare/anchor/vadeclaim/accounts/mints").Select(o=>new PublicKey(JsonConvert.DeserializeAnonymousType(File.ReadAllText(o), key).pubkey)).ToArray();
    }
    

    [TestMethod]
    public async Task TestMethod1()
    {
        var user = new Solnet.KeyStore.SolanaKeyStoreService().RestoreKeystoreFromFile(env["wallet"]);
        Console.WriteLine(mints.Length);
        var mintList = mints.ToList();

        foreach(var mint in mintList){
            var name = await GetNameForMint(mint);
            var instruction = Lib.CreateDepositInstruction(user.Account.PublicKey, mint, name);

            var res1 = await PrepareAndSend(instruction, user.Account);
            Console.WriteLine(res1.RawRpcResponse);
            Console.WriteLine("confirming");
            var confirmed = await TransactionConfirmationUtils.ConfirmTransaction(rpc, ws, res1.Result, Commitment.Processed);

            var instruction2 = Lib.CreateWithdrawInstruction(user.Account.PublicKey, mint, name);
            var res2 = await PrepareAndSend(instruction2, user.Account);
            Console.WriteLine(res1.RawRpcResponse);
            Console.WriteLine(res2.RawRpcResponse);
            return;
        }
    }
    private async Task<string> GetNameForMint(PublicKey mint)
    {
        var metaId = new PublicKey("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
        var acc = await rpc.GetAccountInfoAsync(Lib.DerivePda(metaId, "metadata", metaId, mint).Key, Solnet.Rpc.Types.Commitment.Confirmed);
        var name = Encoding.UTF8.GetString(Convert.FromBase64String(acc.Result.Value.Data[0]).Skip(65).Take(36).ToArray()).Trim('\0').Trim();
        return name;

    }

    private async Task<Solnet.Rpc.Core.Http.RequestResult<string>> PrepareAndSend(TransactionInstruction instruction, Account signer){
        Console.WriteLine(rpc.GetRecentBlockHash(Commitment.Processed).RawRpcResponse);
        return await rpc.SendTransactionAsync(new TransactionBuilder().AddInstruction(instruction).SetFeePayer(signer.PublicKey).SetRecentBlockHash((await rpc.GetLatestBlockHashAsync(Commitment.Finalized)).Result.Value.Blockhash).Build(signer), false, Commitment.Processed);
    }
}