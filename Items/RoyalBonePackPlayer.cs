using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items; 

namespace TheSanity.Systems
{
    public class RoyalBonePackPlayer : ModPlayer
    {
        public bool hasRoyalBonePack = false;
        
        // Timer kustom untuk menjeda kemunculan Tangan Bayangan (Bone Helm)
        private int boneHelmTimer = 0;

        public override void ResetEffects() {
            hasRoyalBonePack = false;
        }

        public override void PostUpdateEquips() {
            if (!hasRoyalBonePack) return;

            Item heldItem = Player.HeldItem;
            
            // Tentukan Jenis Minion yang harus aktif
            bool spawnAll = !heldItem.IsAir && heldItem.damage > 0 && heldItem.DamageType == DamageClass.Default;
            bool spawnMagic = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Magic));
            bool spawnSummon = spawnAll || (!heldItem.IsAir && (heldItem.DamageType.CountsAsClass(DamageClass.Summon) || heldItem.DamageType.CountsAsClass(DamageClass.SummonMeleeSpeed)));
            bool spawnMelee = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Melee));
            bool spawnRanger = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Ranged));

            // Hitung kalkulasi Damage dasar (Base 10 + Scaling Player Stat)
            int dmgMagic = (int)Player.GetTotalDamage(DamageClass.Magic).ApplyTo(10);
            int dmgSummon = (int)Player.GetTotalDamage(DamageClass.Summon).ApplyTo(10);
            int dmgMelee = (int)Player.GetTotalDamage(DamageClass.Melee).ApplyTo(10);
            int dmgRanger = (int)Player.GetTotalDamage(DamageClass.Ranged).ApplyTo(10);

            if (spawnAll) {
                int classlessDmg = (int)(heldItem.damage * 0.5f);
                dmgMagic = dmgSummon = dmgMelee = dmgRanger = classlessDmg;
            }

            // Atur Spawn dan Despawn Minion Kustom secara Realtime
            ManageMinion(ModContent.ProjectileType<BlizzardnadoProj>(), spawnMagic, dmgMagic);
            ManageMinion(ModContent.ProjectileType<SkullytronProj>(), spawnSummon, dmgSummon);
            ManageMinion(ModContent.ProjectileType<CrownySlimeProj>(), spawnMelee, dmgMelee);
            ManageMinion(ModContent.ProjectileType<BabyNetProj>(), spawnRanger, dmgRanger);

            // =========================================================================
            // 🖤 REPLIKA MANUAL EFEK BONE HELM (100% AMAN & LOLOS COMPILER)
            // =========================================================================
            boneHelmTimer++;
            if (boneHelmTimer >= 60) { // Menyerang otomatis setiap 1 detik sekali
                boneHelmTimer = 0;

                if (Player.whoAmI == Main.myPlayer) {
                    NPC target = null;
                    float maxRange = 400f; // Jarak deteksi musuh (sekitar 25 blok)

                    // Cari musuh terdekat yang berada di area jangkauan player
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage) {
                            float distance = Vector2.Distance(Player.Center, npc.Center);
                            if (distance < maxRange) {
                                target = npc;
                                break; // Kunci target pertama yang ditemukan
                            }
                        }
                    }

                    // Jika musuh terdeteksi, panggil tangan shadow langsung di bawah posisi musuh!
                    if (target != null) {
                        // Spawn sedikit acak di bawah musuh agar efek magisnya terasa alami
                        Vector2 spawnPos = target.Center + new Vector2(Main.rand.Next(-20, 21), Main.rand.Next(40, 70));
                        Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 4f;

                        // ProjectileID.InsanityShadowHand (ID: 960) adalah proyektil asli milik Bone Helm vanilla
                        int baseHelmDamage = (int)Player.GetTotalDamage(DamageClass.Summon).ApplyTo(22); 
                        Projectile.NewProjectile(Player.GetSource_Accessory(new Item(ModContent.ItemType<RoyalBonePack>())), spawnPos, velocity, ProjectileID.InsanityShadowFriendly, baseHelmDamage, 1.5f, Player.whoAmI);
                    }
                }
            }
        }

        private void ManageMinion(int projType, bool shouldExist, int damage) {
            if (Player.whoAmI == Main.myPlayer) {
                if (shouldExist) {
                    if (Player.ownedProjectileCounts[projType] <= 0) {
                        Projectile.NewProjectile(Player.GetSource_Accessory(new Item(ModContent.ItemType<RoyalBonePack>())), Player.Center, Vector2.Zero, projType, damage, 2f, Player.whoAmI);
                    }
                }
            }
        }
    }
}