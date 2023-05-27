use std::str::FromStr;
use anchor_lang::prelude::*;
use anchor_spl::{
    metadata::MetadataAccount,
    token::{Mint, Token, TokenAccount}, associated_token::AssociatedToken,
};
use utils::Category;

declare_id!("vadebu9gx5FpP4HQNMdyY51jjTyHbSWCce2RGoGj7mE");

#[program]
pub mod vadeclaim {

    use anchor_lang::solana_program::sysvar::clock;

    use crate::utils::{execute_token_transfer, execute_token_close, execute_token_account_auth};

    use super::*;

    pub fn deposit<'info>(ctx: Context<'_, '_, '_, 'info, Deposit<'info>>) -> Result<()> {
        let counter = &mut ctx.accounts.counter;
        if counter.started_at == 0 {
            counter.started_at = clock::Clock::get().unwrap().unix_timestamp;
        }
        let signer = &ctx.accounts.signer.to_account_info();
        let source_account = &ctx.accounts.source_account;
        let destination_account = &ctx.accounts.destination_account;
        let metadata_info = &ctx.accounts.metadata_account;
        let collection = metadata_info.collection.clone().unwrap();
        if !collection.verified
            || collection
                .key
                .ne(&Pubkey::from_str("HWrURL3FhpuMT3GHypAEvq3AG3Wf2WSDa43A2Y2ehHQk").unwrap())
        {
            panic!("invalid collection")
        };
        let category = utils::get_id_by_name(&metadata_info.data.name)?;

        counter.increment(category)?;

        execute_token_transfer(1, 
            source_account.to_account_info(), 
            destination_account.to_account_info(), 
            signer.to_account_info(), 
            ctx.accounts.token_program.to_account_info(), 
            None)?;
        execute_token_close(source_account.to_account_info(),
            destination_account.to_account_info(), 
            signer.to_account_info(), 
            ctx.accounts.token_program.to_account_info(),
        None)
    }

    pub fn withdraw<'info>(ctx: Context<'_, '_, '_, 'info, Withdraw<'info>>) -> Result<()> {
        let counter = &mut ctx.accounts.counter;
        let signer = &ctx.accounts.signer.to_account_info();
        let source_account = &ctx.accounts.source_account;
        let destination_account = &ctx.accounts.destination_account;
        let metadata_info = &ctx.accounts.metadata_account;
        let collection = metadata_info.collection.clone().unwrap();
        if !collection.verified
            || collection
                .key
                .ne(&Pubkey::from_str("HWrURL3FhpuMT3GHypAEvq3AG3Wf2WSDa43A2Y2ehHQk").unwrap())
        {
            panic!("invalid collection")
        };
        let category = utils::get_id_by_name(&metadata_info.data.name)?;

        counter.decrement(category)?;

        execute_token_transfer(1, 
            source_account.to_account_info(), 
            destination_account.to_account_info(), 
            ctx.accounts.category_auth.to_account_info(), 
            ctx.accounts.token_program.to_account_info(), 
            Some(&[&[signer.key().as_ref(),utils::get_id_from_metadata(&metadata_info).unwrap().get_reward_mint().as_ref(), &[*ctx.bumps.get("category_auth").unwrap()]]]))?;
        execute_token_close(
            source_account.to_account_info(), 
            signer.to_account_info(), 
            ctx.accounts.category_auth.to_account_info(),
            ctx.accounts.token_program.to_account_info(),
            Some(&[&[signer.key().as_ref(),utils::get_id_from_metadata(&metadata_info).unwrap().get_reward_mint().as_ref(), &[*ctx.bumps.get("category_auth").unwrap()]]]))
    }

    pub fn claim_reward<'info>(ctx: Context<'_, '_, '_, 'info,ClaimReward<'info>>, bumps: Vec<u8>) -> Result<()> {
        let mint = ctx.accounts.reward_mint.key();
        
        let category = Category::get_category_for_mint(&mint);
        let token_program = ctx.accounts.token_program.to_account_info().to_owned();
        let category_auth = ctx.accounts.category_auth.to_account_info();
        let expected = ctx.accounts.counter.reset(category)?.clone();
        

        execute_token_transfer(
            1, 
            ctx.accounts.source_account.to_account_info(), 
            ctx.accounts.destination_account.to_account_info(), 
            ctx.accounts.program_auth.to_account_info(), 
            token_program.to_account_info(), 
            Some(&[&[b"auth",&[*ctx.bumps.get("program_auth").unwrap()]]]))?;

        let new_auth = ctx.accounts.program_auth.key();
        let sig = ctx.accounts.signer.key();
        let reward = ctx.accounts.reward_mint.key();
        let binding = [sig.as_ref(),reward.as_ref(),&[*ctx.bumps.get("category_auth").unwrap()]];
        let category_seeds = &[&binding[..]][..];
        let mut i = 0;
        let accs = &mut ctx.remaining_accounts.iter();
        let mut bumps = bumps.iter();
        while i < expected {
            i += 1;
            let acc = next_account_info(accs);
            let bump = *bumps.next().unwrap();
            match acc{
                Ok(acc) => {
                    let token_acc = Account::<TokenAccount>::try_from(acc)?;
                    let found = Pubkey::create_program_address(&[b"nft", token_acc.mint.as_ref(),&[bump]], &id()).unwrap();
                    if acc.key.ne(&found) {
                        return err!(VadeClaimErrors::NotDepositedMint);
                    }
                    execute_token_account_auth(category_auth.to_account_info(), acc.to_account_info(), new_auth, token_program.to_account_info(), Some(category_seeds))?;
                }
                Err(_) => {
                    return err!(VadeClaimErrors::TooFewMints);
                }
            }
        }
        msg!("Done");

        
        if next_account_info(accs).is_ok(){return err!(VadeClaimErrors::TooManyMints);};
        
        
        Ok(())



    }
}

#[derive(Accounts)]
pub struct Deposit<'info> {
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(init_if_needed, space=8+50, payer=signer, seeds=[signer.key().as_ref()], bump)]
    pub counter: Account<'info, Counter>,
    /// CHECK: just a pda
    #[account(seeds=[signer.key().as_ref(), utils::get_id_from_metadata(&metadata_account).unwrap().get_reward_mint().as_ref()], bump)]
    pub category_auth: AccountInfo<'info>,
    #[account(has_one=mint)]
    pub metadata_account: Account<'info, MetadataAccount>,
    #[account(mut, has_one=mint)]
    pub source_account: Account<'info, TokenAccount>,
    #[account(init, token::mint = mint, token::authority = category_auth, payer=signer, seeds=[b"nft", mint.key().as_ref()], bump)]
    pub destination_account: Account<'info, TokenAccount>,
    pub mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct Withdraw<'info> {
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(mut)]
    pub counter: Box<Account<'info, Counter>>,
    /// CHECK: Just a pda, again
    #[account(seeds=[signer.key().as_ref(), utils::get_id_from_metadata(&metadata_account).unwrap().get_reward_mint().as_ref()], bump)]
    pub category_auth: AccountInfo<'info>,
    #[account(has_one=mint)]
    pub metadata_account: Box<Account<'info, MetadataAccount>>,
    #[account(mut, has_one=mint, seeds=[b"nft", mint.key().as_ref()], bump)]
    pub source_account: Account<'info, TokenAccount>,
    #[account(init_if_needed, associated_token::mint=mint, associated_token::authority=signer.to_account_info(), payer=signer)]
    pub destination_account: Account<'info, TokenAccount>,
    pub mint: Account<'info, Mint>,
    pub associated_token_program: Program<'info, AssociatedToken>,
    pub token_program: Program<'info, Token>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ClaimReward<'info>{
    #[account(mut)]
    pub signer: Signer<'info>,
    #[account(mut, seeds=[signer.key().as_ref()], bump)]
    pub counter: Account<'info, Counter>,
    #[account(init_if_needed, payer=signer, associated_token::mint=reward_mint, associated_token::authority = signer )]
    pub destination_account: Account<'info, TokenAccount>,
    pub reward_mint: Account<'info, Mint>,
    #[account(mut, seeds=[reward_mint.key().as_ref()], bump)]
    pub source_account: Account<'info, TokenAccount>,
    /// CHECK: This is just a signer
    #[account(seeds=[signer.key().as_ref(), reward_mint.key().as_ref()], bump)]
    pub category_auth: AccountInfo<'info>,
    /// CHECK: This is just a signer
    #[account(seeds=[b"auth"], bump)]
    pub program_auth: UncheckedAccount<'info>,
    pub associated_token_program: Program<'info, AssociatedToken>,
    pub token_program: Program<'info, Token>,
    pub system_program: Program<'info, System>,
}

#[error_code]
pub enum VadeClaimErrors{
    #[msg("Item Id already collected")]
    ItemIdAlreadyCollected,
    #[msg("Item Id not yet collected")]
    ItemNotYetCollected,
    #[msg("Can't claim without all the pieces, sorry.")]
    SetNotComplete,
    #[msg("Too many mints ser")]
    TooManyMints,
    #[msg("Not enough mints ser")]
    TooFewMints,
    #[msg("Stop injecting random mints")]
    NotDepositedMint,
}

#[account]
pub struct Counter {
    pub animals_collected: [bool; 14],
    pub plants_collected: [bool; 12],
    pub mushrooms_collected: [bool; 8],
    pub artifacts_collected: [bool; 8],
    pub started_at: i64,
}

impl Counter {
    pub fn increment(&mut self, cat: Category) -> Result<()> {
        match cat {
            Category::Animal(val) => {
                if self.animals_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemIdAlreadyCollected)
                }
                self.animals_collected[val as usize] = true;
            }
            Category::Plant(val) => {
                if self.plants_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemIdAlreadyCollected)
                }
                self.plants_collected[val as usize] = true;
            }
            Category::Mushroom(val) => {
                if self.mushrooms_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemIdAlreadyCollected)
                }
                self.mushrooms_collected[val as usize] = true;
            }
            Category::Artifact(val) => {
                if self.artifacts_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemIdAlreadyCollected)
                }
                self.artifacts_collected[val as usize] = true;
            }
        }
        Ok(())
    }

    pub fn decrement(&mut self, cat: Category) -> Result<()> {
        match cat {
            Category::Animal(val) => {
                if !self.animals_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemNotYetCollected)
                }
                self.animals_collected[val as usize] = false;
            }
            Category::Plant(val) => {
                if !self.plants_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemNotYetCollected)
                }
                self.plants_collected[val as usize] = false;
            }
            Category::Mushroom(val) => {
                if !self.mushrooms_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemNotYetCollected)
                }
                self.mushrooms_collected[val as usize] = false;
            }
            Category::Artifact(val) => {
                if !self.artifacts_collected[val as usize] {
                    return err!(VadeClaimErrors::ItemNotYetCollected)
                }
                self.artifacts_collected[val as usize] = false;
            }
        }
        Ok(())
    }

    pub fn reset(&mut self, cat: Category) -> Result<u8> {
        match cat {
            Category::Animal(_) => {
                if self.animals_collected.iter().all(|&x| x) {
                    self.animals_collected.clone_from_slice(&[false; 14]);
                    Ok(14)
                } else {
                    err!(VadeClaimErrors::SetNotComplete)
                }
            }
            Category::Plant(_) => {
                if self.plants_collected.iter().all(|&x| x) {
                    self.plants_collected.clone_from_slice(&[false; 12]);
                    Ok(12)
                } else {
                    err!(VadeClaimErrors::SetNotComplete)
                }
            }
            Category::Mushroom(_) => {
                if self.mushrooms_collected.iter().all(|&x| x) {
                    self.mushrooms_collected.clone_from_slice(&[false; 8]);
                    Ok(8)
                } else {
                    err!(VadeClaimErrors::SetNotComplete)
                }
            }
            Category::Artifact(_) => {
                if self.artifacts_collected.iter().all(|&x| x) {
                    self.artifacts_collected.clone_from_slice(&[false; 8]);
                    Ok(8)
                } else {
                    err!(VadeClaimErrors::SetNotComplete)
                }
            }
        }
    }
}

pub mod utils {
    use anchor_lang::{
        prelude::*,
    };
    use anchor_spl::{
        metadata::{self, MetadataAccount},
        token::{TokenAccount},
    };
    use std::str::FromStr;

    pub fn get_id_by_name(name: &str) -> Result<Category> {
        let split: Vec<&str> = name.trim_end_matches('\0').split('#').collect();
        if split.len() != 2 {
            panic!("invalid string format");
        }
        let mut number = match split[1].parse::<u32>() {
            Ok(n) => n,
            Err(_) => panic!("invalid number format"),
        };
        let (animal_count, plants_count, mushrooms_count, artifacts_count) = (1400, 960, 480, 320);
        let (animal_series_len, plants_series_len, mushroom_series_len, artifacts_series_len) =
            (100, 80, 60, 40);
        let mut _offset = 0;
        if number < animal_count {
            return Ok(Category::Animal(number / animal_series_len));
        }
        _offset += animal_count / animal_series_len;
        number -= animal_count;
        if number < plants_count {
            return Ok(Category::Plant(number / plants_series_len));
        }
        _offset += plants_count / plants_series_len;
        number -= plants_count;
        if number < mushrooms_count {
            return Ok(Category::Mushroom(number / mushroom_series_len));
        }
        _offset += mushrooms_count / mushroom_series_len;
        number -= mushrooms_count;
        if number < artifacts_count {
            return Ok(Category::Artifact(number / artifacts_series_len));
        }
        panic!("invalid string")
    }

    pub fn get_id_from_metadata(metadata_account: &Account<MetadataAccount>) -> Result<Category> {
        get_id_by_name(&metadata_account.data.name)
    }

    #[derive(Debug)]
    pub struct AccountSet<'info> {
        pub metadata_acc: Account<'info, MetadataAccount>,
        pub token_acc: Account<'info, TokenAccount>,
        pub mint_acc: AccountInfo<'info>,
        pub category: Category,
    }


    #[derive(Debug, PartialEq, Eq, AnchorSerialize, AnchorDeserialize, Clone, Copy)]
    pub enum Category {
        Animal(u32),
        Plant(u32),
        Mushroom(u32),
        Artifact(u32),
    }
    impl Category {
        pub fn get_reward_mint(&self) -> Pubkey {
            Pubkey::from_str(match self {
                Category::Animal(_) => "BNotnj4DtUTMaYK9qHRnWMPKnkYQ6cM2yiGGcJ9aAsVh",
                Category::Plant(_) => "9Biry698BLiU1XsJpyRzVa7iwv3fJGWXnFrMeTip8m8u",
                Category::Mushroom(_) => "BKny8BzDh6kZKpB8uySNcMyhVe9NcEBoHoAMG4RQCKoW",
                Category::Artifact(_) => "7VqorQ1hPSnTzz3s5qDHsSc7bL5ZcwAVxajwdDtiCNJX",
            })
            .unwrap()
        }
        pub fn get_category_for_mint(pubkey: &Pubkey) -> Category {
            match pubkey.to_string().as_str() {
                "BNotnj4DtUTMaYK9qHRnWMPKnkYQ6cM2yiGGcJ9aAsVh" => Category::Animal(0), 
                "9Biry698BLiU1XsJpyRzVa7iwv3fJGWXnFrMeTip8m8u" => Category::Plant(0), 
                "BKny8BzDh6kZKpB8uySNcMyhVe9NcEBoHoAMG4RQCKoW" => Category::Mushroom(0), 
                "7VqorQ1hPSnTzz3s5qDHsSc7bL5ZcwAVxajwdDtiCNJX" => Category::Artifact(0), 
                _ => panic!("Invalid mint pubkey")
            }
        }
    }

    pub fn execute_nft_burn<'info>(
        owner: AccountInfo<'info>,
        mint: AccountInfo<'info>,
        metadata: AccountInfo<'info>,
        edition: AccountInfo<'info>,
        token: AccountInfo<'info>,
        spl_token: AccountInfo<'info>,
        metadata_program: AccountInfo<'info>,
        collection_metadata: AccountInfo<'info>,
    ) -> Result<()> {
        let collection_key = collection_metadata.key();
        let accounts = metadata::BurnNft {
            metadata,
            owner,
            mint,
            token,
            edition,
            spl_token,
            collection_metadata,
        };
        let ctx = CpiContext::new(metadata_program, accounts);
        metadata::burn_nft(ctx, Some(collection_key))
    }

    pub fn execute_token_transfer<'a>(
        amount: u64,
        from: AccountInfo<'a>,
        to: AccountInfo<'a>,
        authority: AccountInfo<'a>,
        token_program: AccountInfo<'a>,
        signer_seeds: Option<&[&[&[u8]]]>,
    ) -> Result<()> {
        let accounts = anchor_spl::token::Transfer {
            from,
            to,
            authority,
        };
        let ctx = CpiContext::new(token_program, accounts);
        anchor_spl::token::transfer(
            match signer_seeds {
                Some(seeds) => ctx.with_signer(seeds),
                None => ctx,
            },
            amount,
        )
    }
    pub fn execute_token_close<'a>(
        account: AccountInfo<'a>,
        destination: AccountInfo<'a>,
        authority: AccountInfo<'a>,
        token_program: AccountInfo<'a>,
        signer_seeds: Option<&[&[&[u8]]]>
    ) -> Result<()> {
        let accounts = anchor_spl::token::CloseAccount {
            account,
            destination,
            authority
        };
        let ctx = CpiContext::new(token_program, accounts);
        anchor_spl::token::close_account(match signer_seeds {
            Some(seeds) => ctx.with_signer(seeds),
            None => ctx,
        })
    }
    pub fn execute_token_account_auth<'a>(
        current_authority: AccountInfo<'a>,
        account: AccountInfo<'a>,
        new_authority: Pubkey,
        token_program: AccountInfo<'a>,
        signer_seeds: Option<&[&[&[u8]]]>
    ) -> Result<()> {
        let accounts = anchor_spl::token::SetAuthority {
            current_authority,
            account_or_mint: account
        };
        let ctx = CpiContext::new(token_program, accounts);
        anchor_spl::token::set_authority(match signer_seeds {
            Some(seeds) => ctx.with_signer(seeds),
            None => ctx,
        }, anchor_spl::token::spl_token::instruction::AuthorityType::AccountOwner, Some(new_authority))
    }
}
