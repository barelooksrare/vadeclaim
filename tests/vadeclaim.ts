import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { Vadeclaim } from "../target/types/vadeclaim";
import { ASSOCIATED_PROGRAM_ID, TOKEN_PROGRAM_ID } from "@coral-xyz/anchor/dist/cjs/utils/token";
import { base64, bs58 } from "@coral-xyz/anchor/dist/cjs/utils/bytes";
import * as mpl from "@metaplex-foundation/mpl-token-metadata";
import { Metadata, Metaplex, tokenProgram } from "@metaplex-foundation/js";
import { PublicKey } from "@metaplex-foundation/js";
import { token } from "@coral-xyz/anchor/dist/cjs/utils";
import fs from "fs";
const MY_PROGRAM_ID = new PublicKey("vadebu9gx5FpP4HQNMdyY51jjTyHbSWCce2RGoGj7mE");

const getMetadataAccount = (mint: PublicKey) => {
  return anchor.web3.PublicKey.findProgramAddressSync([Buffer.from("metadata"), mpl.PROGRAM_ID.toBuffer(), mint.toBuffer()], mpl.PROGRAM_ID)[0];
}

const getCounterAccount = (user: PublicKey) => {
  return anchor.web3.PublicKey.findProgramAddressSync([user.toBuffer()], MY_PROGRAM_ID)[0];
}


const getCategoryAuthAccount = (user: PublicKey, category: Category) => {
  return anchor.web3.PublicKey.findProgramAddressSync([user.toBuffer(), getCategoryMint(category).toBuffer()], MY_PROGRAM_ID)[0];
}

const getAuthAccount = () => {
  return anchor.web3.PublicKey.findProgramAddressSync([Buffer.from("auth")], MY_PROGRAM_ID)[0];
}


const getAuthTokenAccount = (mint: PublicKey) => {
  return anchor.web3.PublicKey.findProgramAddressSync([mint.toBuffer()], MY_PROGRAM_ID)[0];
}

const getDepositTokenAccount = (mint: PublicKey) => {
  return anchor.web3.PublicKey.findProgramAddressSync([Buffer.from("nft"),mint.toBuffer()], MY_PROGRAM_ID);
}

enum Category {
  ANIMAL = "animal",
  PLANT = "plant",
  MUSHROOM = "mushroom",
  ARTIFACT = "artifact"
}

const CategoryIndex = [
  Category.ANIMAL,
  Category.PLANT,
  Category.MUSHROOM,
  Category.ARTIFACT,
]

const categoryMap = {
  "animal": {animal: {0: 0}}, 
  "plant": {plant: {0: 0}}, 
  "mushroom": {mushroom: {0: 0}}, 
  "artifact": {artifact: {0: 0}}
}

function getIdByName(name: string): Category {
  const split = name.trim().split('#');
  if (split.length !== 2) {
      throw new Error("invalid string format");
  }
  let number = Number.parseInt(split[1]);
  if (isNaN(number)) {
      throw new Error("invalid number format");
  }

  const animalCount = 1400;
  const plantsCount = 960;
  const mushroomsCount = 480;
  const artifactsCount = 320;

  const animalSeriesLen = 100;
  const plantsSeriesLen = 80;
  const mushroomSeriesLen = 60;
  const artifactsSeriesLen = 40;

  let _offset = 0;

  if (number < animalCount) {
      return Category.ANIMAL;
  }
  _offset += animalCount / animalSeriesLen;
  number -= animalCount;

  if (number < plantsCount) {
      return Category.PLANT;
  }
  _offset += plantsCount / plantsSeriesLen;
  number -= plantsCount;

  if (number < mushroomsCount) {
      return Category.MUSHROOM;
  }
  _offset += mushroomsCount / mushroomSeriesLen;
  number -= mushroomsCount;

  if (number < artifactsCount) {
      return Category.ARTIFACT;
  }

  throw new Error("invalid string");
}

const getCategoryMint = (type: Category): PublicKey => {
  switch (type) {
    case "animal":
      return new PublicKey("BNotnj4DtUTMaYK9qHRnWMPKnkYQ6cM2yiGGcJ9aAsVh");
    case "plant":
      return new PublicKey("9Biry698BLiU1XsJpyRzVa7iwv3fJGWXnFrMeTip8m8u");
    case "mushroom":
      return new PublicKey("BKny8BzDh6kZKpB8uySNcMyhVe9NcEBoHoAMG4RQCKoW");
    case "artifact":
      return new PublicKey("7VqorQ1hPSnTzz3s5qDHsSc7bL5ZcwAVxajwdDtiCNJX");
    default:
      throw new Error("Invalid type");
  }
};

const getAta = (mint: PublicKey, owner: PublicKey) => {
  return token.associatedAddress({mint, owner});
}




describe("vadeclaim", () => {
  // Configure the client to use the local cluster.
  anchor.setProvider(anchor.AnchorProvider.env());
  const connection = anchor.AnchorProvider.env().connection;

  const program = anchor.workspace.Vadeclaim as Program<Vadeclaim>;

  console.log(new anchor.BN(1).toString());

  const mints : string[] = fs.readdirSync("./accounts/mints/", {withFileTypes: false, recursive: false}).map(o=>o.toString().replace(".json", ""));

  //console.log(mints);
  it("Deposits and increments!", async () => {
    // Add your test here.
    for(let i = 0; i < 10000;){
      console.log(mints.length);
      const mint = new anchor.web3.PublicKey(mints.shift());

      const metadataAddress = getMetadataAccount(mint);
      const metadataAccountInfo = await mpl.Metadata.fromAccountAddress(connection, metadataAddress);
      const name = metadataAccountInfo.data.name;

      const category = getIdByName(name);

      const user = anchor.getProvider().publicKey;
      const categoryAuthAcc = getCategoryAuthAccount(user, category);

      try{
      const tx = await program.methods.deposit().accounts({
        counter: getCounterAccount(user),
        sourceAccount: getAta(mint, user),
        destinationAccount: getDepositTokenAccount(mint)[0],
        metadataAccount: getMetadataAccount(mint),
        mint: mint,
        categoryAuth: categoryAuthAcc,
      }).rpc();

      console.log(++i);
      let counterAcc = await program.account.counter.fetch(getCounterAccount(user));
      console.log(counterAcc);

      console.log(i%3);
      if(i%3===0){
        console.log("withdrawing");
        const withdrawTx = await program.methods.withdraw().accounts({
          counter: getCounterAccount(user),
          sourceAccount: getDepositTokenAccount(mint)[0],
          destinationAccount: getAta(mint, user),
          metadataAccount: getMetadataAccount(mint),
          mint: mint,
          categoryAuth: categoryAuthAcc,
          associatedTokenProgram: ASSOCIATED_PROGRAM_ID,
          tokenProgram: TOKEN_PROGRAM_ID,
        }).rpc();

        mints.push(mint.toBase58());
      }

      const boolArrays = [counterAcc.animalsCollected, counterAcc.plantsCollected, counterAcc.mushroomsCollected, counterAcc.artifactsCollected];
      const fullSetIndex = boolArrays.findIndex(o=>o.every(i=>i === true));
    
      if(fullSetIndex != -1){
        const category = CategoryIndex[fullSetIndex];
        const categoryAuth = getCategoryAuthAccount(user, category);
        const tokenAccounts = await anchor.getProvider().connection.getTokenAccountsByOwner(categoryAuth, {programId: TOKEN_PROGRAM_ID},{commitment: "processed"});
        
        const bumps = Buffer.from(tokenAccounts.value.map(o=>getDepositTokenAccount(new anchor.web3.PublicKey(o.account.data.subarray(0, 32)))[1]));

        const rewardMint = getCategoryMint(category);
        console.log("claiming", category);
        console.log("no of token accounts", tokenAccounts.value.length);
        try{const tx2 = await program.methods.claimReward(bumps).accounts({
          counter: getCounterAccount(user),
          signer: user,
          sourceAccount: getAuthTokenAccount(rewardMint),
          rewardMint,
          programAuth: getAuthAccount(),
          categoryAuth: getCategoryAuthAccount(user, category),
          destinationAccount: getAta(rewardMint, user),
        }).remainingAccounts(tokenAccounts.value.map(o=>({pubkey: o.pubkey, isWritable: true, isSigner: false}))).rpc();
      }
      catch(e){
         throw e;
      }
        counterAcc = await program.account.counter.fetch(getCounterAccount(user));
      }
    }
    catch(e){
      mints.push(mint.toBase58());
      const code = e.error.errorCode.code;
      if(code !== "ItemIdAlreadyCollected" && code !== "SetNotComplete")
      {
        if(e.errorLogs[0] !== 'Program log: AnchorError caused by account: source_account. Error Code: AccountNotInitialized. Error Number: 3012. Error Message: The program expected this account to be already initialized.'){
          
          throw e;
        }
        else{
          mints.pop();
        }
      }
    }
  }});
  it("withdraws", async => ({

  }));
});
