using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Vadeclaim.Program;
using Vadeclaim.Types;

namespace Vadeclaim.Utils
{
    public static class Lib
    {
        public struct KeyWithBump
        {
            public PublicKey Key { get; }
            public byte Bump { get; }

            public KeyWithBump(PublicKey key, byte bump)
            {
                Key = key;
                Bump = bump;
            }

            public static implicit operator PublicKey(KeyWithBump k) => k.Key;
        }

        private static readonly Dictionary<Category, PublicKey> categoryMintMap = new Dictionary<Category, PublicKey>
        {
            {Category.Animal, new PublicKey("BNotnj4DtUTMaYK9qHRnWMPKnkYQ6cM2yiGGcJ9aAsVh")},
            {Category.Plant, new PublicKey("9Biry698BLiU1XsJpyRzVa7iwv3fJGWXnFrMeTip8m8u")},
            {Category.Mushroom, new PublicKey("BKny8BzDh6kZKpB8uySNcMyhVe9NcEBoHoAMG4RQCKoW")},
            {Category.Artifact, new PublicKey("7VqorQ1hPSnTzz3s5qDHsSc7bL5ZcwAVxajwdDtiCNJX")}
        };

        private static PublicKey PROGRAM_ID = new PublicKey("vadebu9gx5FpP4HQNMdyY51jjTyHbSWCce2RGoGj7mE");
        private static PublicKey TOKEN_METADATA_PROGRAM = new PublicKey("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");

        private static KeyWithBump GetMetadataAccount(PublicKey mint)
        {
            return DerivePda(TOKEN_METADATA_PROGRAM, "metadata", TOKEN_METADATA_PROGRAM, mint);
        }

        private static KeyWithBump GetCounterAccount(PublicKey user)
        {
            return DerivePda(PROGRAM_ID, user);
        }
        
        private static KeyWithBump GetCategoryAuthAccount(PublicKey user, Category category)
        {
            PublicKey categoryMint = GetCategoryMint(category);
            return DerivePda(PROGRAM_ID, user, categoryMint);
        }

        private static KeyWithBump GetAuthAccount()
        {
            return DerivePda(PROGRAM_ID, "auth");
        }

        private static KeyWithBump GetAuthTokenAccount(PublicKey mint)
        {
            return DerivePda(PROGRAM_ID, mint);
        }

        private static KeyWithBump GetDepositTokenAccount(PublicKey mint)
        {
            return DerivePda(PROGRAM_ID, "nft", mint);
        }

        public static KeyWithBump DerivePda(PublicKey programId, params object[] items)
        {
            List<byte[]> seeds = new List<byte[]>();
            foreach (var item in items)
            {
                if (item.GetType() == typeof(string)) seeds.Add(Encoding.UTF8.GetBytes((string)item));
                if (item.GetType() == typeof(PublicKey)) seeds.Add(((PublicKey)item).KeyBytes);
                if (item.GetType() == typeof(byte[])) seeds.Add((byte[])item);
            }
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey key, out byte bump);
            return new KeyWithBump(key, bump);
        }

        private static PublicKey GetCategoryMint(Category category)
        {
            if (!categoryMintMap.ContainsKey(category))
            {
                throw new ArgumentException("Invalid type");
            }
            return categoryMintMap[category];
        }

        private static Category GetCategoryByName(string name)
        {
            string[] split = name.Trim().Split('#');
            if (split.Length != 2)
            {
                throw new ArgumentException("Invalid string format");
            }

            if (!int.TryParse(split[1], out int number))
            {
                throw new ArgumentException("Invalid number format");
            }

            int animalCount = 1400;
            int plantsCount = 960;
            int mushroomsCount = 480;
            int artifactsCount = 320;

            int animalSeriesLen = 100;
            int plantsSeriesLen = 80;
            int mushroomSeriesLen = 60;
            int artifactsSeriesLen = 40;

            int _offset = 0;

            if (number < animalCount)
            {
                return Category.Animal;
            }
            _offset += animalCount / animalSeriesLen;
            number -= animalCount;

            if (number < plantsCount)
            {
                return Category.Plant;
            }
            _offset += plantsCount / plantsSeriesLen;
            number -= plantsCount;

            if (number < mushroomsCount)
            {
                return Category.Mushroom;
            }
            _offset += mushroomsCount / mushroomSeriesLen;
            number -= mushroomsCount;

            if (number < artifactsCount)
            {
                return Category.Artifact;
            }

            throw new ArgumentException("Invalid string");
        }

        public static TransactionInstruction CreateDepositInstruction(PublicKey user, PublicKey mint, string onchainItemName, PublicKey sourceTokenAccount = null)
        {
            sourceTokenAccount = sourceTokenAccount ?? AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(user, mint);

            var accounts = new DepositAccounts()
            {
                Signer = user,
                Counter = GetCounterAccount(user),
                CategoryAuth = GetCategoryAuthAccount(user, GetCategoryByName(onchainItemName)),
                MetadataAccount = GetMetadataAccount(mint),
                SourceAccount = sourceTokenAccount,
                DestinationAccount = GetDepositTokenAccount(mint),
                Mint = mint,
                TokenProgram = TokenProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey,
            };
            return VadeclaimProgram.Deposit(accounts, PROGRAM_ID);
        }

        public static TransactionInstruction CreateWithdrawInstruction(PublicKey user, PublicKey mint, string onchainItemName)
        {
            var accounts = new WithdrawAccounts
            {
                Signer = user,
                Counter = GetCounterAccount(user),
                CategoryAuth = GetCategoryAuthAccount(user, GetCategoryByName(onchainItemName)),
                MetadataAccount = GetMetadataAccount(mint),
                SourceAccount = GetDepositTokenAccount(mint),
                DestinationAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(user, mint),
                Mint = mint,
                AssociatedTokenProgram = AssociatedTokenAccountProgram.ProgramIdKey,
                TokenProgram = TokenProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey
            };
            return VadeclaimProgram.Withdraw(accounts, PROGRAM_ID);
        }

        public static TransactionInstruction CreateClaimRewardInstruction(PublicKey user, Category category, List<PublicKey> mints){
            var rewardMint = GetCategoryMint(category);
            var accounts = new ClaimRewardAccounts()
            {
                Signer = user,
                Counter = GetCounterAccount(user),
                DestinationAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(user, rewardMint),
                RewardMint = rewardMint,
                SourceAccount = GetAuthTokenAccount(rewardMint),
                CategoryAuth = GetCategoryAuthAccount(user, category),
                ProgramAuth = GetAuthAccount(),
                AssociatedTokenProgram = AssociatedTokenAccountProgram.ProgramIdKey,
                TokenProgram = TokenProgram.ProgramIdKey,
                SystemProgram = SystemProgram.ProgramIdKey,
            };
            var items = mints.Select(o=>GetAuthTokenAccount(o));
            var bumps = items.Select(o=>o.Bump).ToArray();
            var instruction = VadeclaimProgram.ClaimReward(accounts, bumps, PROGRAM_ID);

            foreach(var item in items){
                instruction.Keys.Add(AccountMeta.Writable(item.Key, false));
            }
            return instruction;
        }
    }

    public enum Category
    {
        Animal,
        Plant,
        Mushroom,
        Artifact
    }
}
