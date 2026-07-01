using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using System;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class TwinkleTwinkle : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/TwinkleTwinkle";

        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Item.type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 5; 
            Item.DamageType = DamageClass.Summon;
            Item.mana = 3;
            Item.width = 42;
            Item.height = 42;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.value = Item.buyPrice(0, 10, 0, 0);
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>(); 
            Item.UseSound = SoundID.Item44;

            Item.buffType = ModContent.BuffType<Projectiles.TwinkleWeapon.TwinkleTwinkleBuff>();
            Item.shoot = ModContent.ProjectileType<Projectiles.TwinkleWeapon.TwinkleTwinkleMinion>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            
            // MEKANIK ALA ABIGAIL: Cari apakah minion ini sudah pernah di-summon sebelumnya
            int currentMinionIndex = -1;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type && Main.projectile[i].owner == player.whoAmI) {
                    currentMinionIndex = i;
                    break;
                }
            }

            if (currentMinionIndex != -1) {
                // JIKA MINION SUDAH ADA: Upgradable / Naik Level Slot
                Projectile p = Main.projectile[currentMinionIndex];
                
                // Hitung total slot minion yang sedang dipakai secara keseluruhan
                float usedSlots = 0f;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI && Main.projectile[i].minion) {
                        usedSlots += Main.projectile[i].minionSlots;
                    }
                }

                // Jika player masih punya slot kosong dan level minion belum mentok (max 20)
                if (usedSlots < player.maxMinions && p.minionSlots < 20f) {
                    p.minionSlots += 1f; // Naikkan slot/level minion ini
                    p.netUpdate = true;
                } 
                // Jika slot penuh, tapi ada minion jenis LAIN yang bisa dikorbankan (Sacrificable)
                else if (p.minionSlots < 20f) {
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile other = Main.projectile[i];
                        if (other.active && other.owner == player.whoAmI && other.type != type && ProjectileID.Sets.MinionSacrificable[other.type]) {
                            other.Kill(); // Hancurkan minion lain tersebut
                            p.minionSlots += 1f; // Alihkan slotnya ke Twinkle Minion
                            p.netUpdate = true;
                            break;
                        }
                    }
                }
                return false; // Batalkan pembuatan objek baru agar tetap berjumlah SATU objek saja
            }

            // JIKA BELUM ADA: Spawn pertama kali di posisi kursor mouse (Mulai dari Level 1 / 1 Slot)
            var spawnedProj = Projectile.NewProjectileDirect(source, Main.MouseWorld, velocity, type, damage, knockback, player.whoAmI);
            spawnedProj.originalDamage = Item.damage;
            
            return false;
        }
    }
}