using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class CameraPlayer : ModPlayer
    {
        public bool HasCameraAccessory = false;

        // ==================== SISTEM COOLDOWN BARU (ICD) ====================
        public int minionSpawnCooldown = 0; // Mengontrol jeda spawn minion
        public int debuffCooldown = 0;      // Mengontrol jeda aplikasi debuff

        public override void ResetEffects() {
            HasCameraAccessory = false;
        }

        // Hook bawaan tModLoader untuk mengurangi angka timer cooldown setiap frame (60 FPS)
        public override void PostUpdateEquips() {
            if (minionSpawnCooldown > 0) minionSpawnCooldown--;
            if (debuffCooldown > 0) debuffCooldown--;
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (HasCameraAccessory) {
                modifiers.Knockback *= 0.80f; // 20% KB Resist
            }
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (HasCameraAccessory) {
                modifiers.Knockback *= 0.80f; // 20% KB Resist
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!HasCameraAccessory) return;

            ApplyCameraDebuffs(target);
            TrySpawnMinions(target.Center);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            // BUGFIX: Ditambahkan ModContent.ProjectileType<RetRubyBolt>() ke dalam filter!
            // Sekarang serangan laser dari minion kita sendiri tidak akan memicu efek aksesoris player.
            if (!HasCameraAccessory || 
                proj.type == ModContent.ProjectileType<Spazamini>() || 
                proj.type == ModContent.ProjectileType<Retanimini>() ||
                proj.type == ModContent.ProjectileType<RetRubyBolt>()) 
                return;

            ApplyCameraDebuffs(target);
            TrySpawnMinions(target.Center);
        }

        private void ApplyCameraDebuffs(NPC target) {
            // Jika debuff baru saja diaplikasikan, kunci sementara agar tidak spam di senjata super cepat
            if (debuffCooldown > 0) return;

            if (Main.rand.NextFloat() < 0.30f) { 
                target.AddBuff(BuffID.CursedInferno, 180); 
                target.AddBuff(BuffID.Ichor, 180); 
                
                debuffCooldown = 90; // Kunci selama 1.5 detik (90 Ticks) sebelum bisa berpeluang aktif lagi
            }
        }

        private void TrySpawnMinions(Vector2 spawnPos) {
            // Jika masih dalam masa jeda Cooldown global, batalkan proses spawn secara mutlak
            if (minionSpawnCooldown > 0) return;

            // Cek apakah minion lama masih aktif berkeliaran di world
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && (p.type == ModContent.ProjectileType<Spazamini>() || p.type == ModContent.ProjectileType<Retanimini>())) {
                    return; 
                }
            }

            if (Main.rand.NextFloat() >= 0.10f) return; // Roll 10% Chance asli

            Item heldItem = Player.HeldItem;
            int baseDmg = 10;
            int finalCrit = 10;

            if (heldItem.type != ItemID.None && heldItem.damage > 0 && heldItem.createTile == -1) {
                baseDmg = heldItem.damage;
                finalCrit = heldItem.crit + (int)Player.GetCritChance(heldItem.DamageType);
            }

            var source = Player.GetSource_FromThis();

            // Spawn Mini Spaz
            int spaz = Projectile.NewProjectile(source, spawnPos + new Vector2(-20, -40), Vector2.Zero, ModContent.ProjectileType<Spazamini>(), baseDmg, 2f, Player.whoAmI);
            if (spaz != Main.maxProjectiles) {
                Main.projectile[spaz].CritChance = finalCrit;
            }

            // Spawn Mini Ret
            int ret = Projectile.NewProjectile(source, spawnPos + new Vector2(20, -40), Vector2.Zero, ModContent.ProjectileType<Retanimini>(), baseDmg, 2f, Player.whoAmI);
            if (ret != Main.maxProjectiles) {
                Main.projectile[ret].CritChance = finalCrit;
            }

            // SET INTERNAL COOLDOWN (ICD): 
            // 2400 Ticks = 40 Detik Cooldown total setelah minion lahir.
            // Karena umur minion di CameraMinions adalah 20 detik, kini akan ada jeda waktu kosong (downtime) 
            // selama 20 detik penuh di mana minion benar-benar menghilang sebelum bisa dipanggil lagi!
            minionSpawnCooldown = 2400; 
        }
    }
}