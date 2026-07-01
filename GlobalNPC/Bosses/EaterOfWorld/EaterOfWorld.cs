using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using Terraria.DataStructures;
using Terraria.Audio;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class ReworkedEaterOfWorld : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool isOriginalImmunePart = false;
        private bool checkedImmunity = false;
        private int eyeFireCooldown = 0;
        private int babyEaterTimer = -1;
        public int retreatTimer = 0;
        public int headState = 0;
        public int dashWindupTimer = 0;
        private int bodyShootTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.EaterofWorldsHead ||
                   entity.type == NPCID.EaterofWorldsBody ||
                   entity.type == NPCID.EaterofWorldsTail;
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) { }

        public override void PostAI(NPC npc)
        {
            if (EaterOfWorldsHealthManager.DeathAnimationActive)
                return;

            npc.TargetClosest(true);
            Player target = null;
            if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target != null && target.active && !target.dead)
            {
                npc.timeLeft = 600;
            }
            else
            {
                return;
            }

            if (!checkedImmunity)
            {
                checkedImmunity = true;
                if (npc.type == NPCID.EaterofWorldsHead && npc.ai[0] == 0)
                {
                    isOriginalImmunePart = true;
                    int nextSegmentIdx = (int)npc.ai[1];
                    for (int i = 0; i < 2; i++)
                    {
                        if (nextSegmentIdx >= 0 && nextSegmentIdx < Main.maxNPCs)
                        {
                            NPC bodyPart = Main.npc[nextSegmentIdx];
                            if (bodyPart.active && (bodyPart.type == NPCID.EaterofWorldsBody || bodyPart.type == NPCID.EaterofWorldsTail))
                            {
                                bodyPart.GetGlobalNPC<ReworkedEaterOfWorld>().isOriginalImmunePart = true;
                                bodyPart.GetGlobalNPC<ReworkedEaterOfWorld>().checkedImmunity = true;
                                nextSegmentIdx = (int)bodyPart.ai[1];
                            }
                        }
                    }
                }
            }

            if (isOriginalImmunePart)
            {
                npc.dontTakeDamage = true;
                npc.alpha = 150;
                if (Main.rand.NextBool(6))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Demonite, 0f, 0f, 150, default, 1.1f);
                }
            }

            // LOGIKA UTAMA SIKLUS AI KEPALA EOW
            if (npc.type == NPCID.EaterofWorldsHead)
            {
                int sisaSegmen = HitungSisaSegmen(npc);
                bool isCacingKecil = sisaSegmen < 22;
                float jarakKePlayer = Vector2.Distance(npc.Center, target.Center);

                if (headState == 0)
                {
                    // --------------------------------------------------------------------------
                    // [BALANCE GUIDE: KECEPATAN KEJAR KEPALA]
                    // --------------------------------------------------------------------------
                    float kecepatanTarget = isCacingKecil ? 11f : 8.5f; // <--- Nilai Speed saat mengejar player
                    float dayaBelokNatural = isCacingKecil ? 0.042f : 0.065f; // <--- Kelincahan belok cacing
                    Vector2 posisiIncaran = target.Center;
                    if (isCacingKecil)
                    {
                        float waktuPrediksi = jarakKePlayer / kecepatanTarget;
                        posisiIncaran = target.Center + (target.velocity * waktuPrediksi * 0.75f);
                    }
                    else if (jarakKePlayer > 700f)
                    {
                        kecepatanTarget = 11f; // <--- Nilai Speed jika player terlalu jauh
                    }

                    // --------------------------------------------------------------------------

                    Vector2 arahKeIncaran = (posisiIncaran - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity = Vector2.Lerp(npc.velocity, arahKeIncaran * kecepatanTarget, dayaBelokNatural);
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    float batas30Block = 30 * 16f;
                    if (jarakKePlayer <= batas30Block)
                    {
                        headState = 1;
                        dashWindupTimer = 45; // [BALANCE GUIDE] Durasi ancang-ancang sebelum dash (dalam hitungan tick)
                        npc.netUpdate = true;

                        SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // --------------------------------------------------------------------------
                            // [BALANCE GUIDE: ATTACK - VILESPIKESEED]
                            // --------------------------------------------------------------------------
                            int seedDamage = 15; // <--- DAMAGE proyektil VileSpikeSeed kepala
                            float seedSpeed = 8.5f; // <--- SPEED proyektil VileSpikeSeed kepala
                            Vector2 targetDiBawahPlayer = new Vector2(target.Center.X, target.Center.Y + 48f);
                            Vector2 velUnderPlayer = (targetDiBawahPlayer - npc.Center).SafeNormalize(Vector2.UnitY) * seedSpeed;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velUnderPlayer, ModContent.ProjectileType<VileSpikeSeed>(), seedDamage, 1f, Main.myPlayer);
                            // --------------------------------------------------------------------------
                        }
                    }

                    HandleHeadAttacks(npc, target, false);
                }
                else if (headState == 1)
                {
                    dashWindupTimer--;
                    // [BALANCE GUIDE] Kecepatan lambat saat ancang-ancang melesat
                    float speedWindupMulus = isCacingKecil ? 7f : 5f; 
                    Vector2 arahAncang = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity = Vector2.Lerp(npc.velocity, arahAncang * speedWindupMulus, 0.06f);
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    if (Main.rand.NextBool(3))
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.CursedTorch, 0f, 0f, 100, default, 1.2f);
                    }

                    if (dashWindupTimer <= 0)
                    {
                        headState = 2;
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                        // --------------------------------------------------------------------------
                        // [BALANCE GUIDE: SPEED DASH KEPALA]
                        // --------------------------------------------------------------------------
                        Vector2 arahDash = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                        float speedDash = isCacingKecil ? 21f : 17f; // <--- KECEPATAN Melesat (Dash) Kepala
                        npc.velocity = arahDash * speedDash;
                        npc.netUpdate = true;
                        // --------------------------------------------------------------------------

                        for (int i = 0; i < 20; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.CursedTorch, npc.velocity.X * 0.3f, npc.velocity.Y * 0.3f, 100, default, 1.5f);
                        }
                    }

                    HandleHeadAttacks(npc, target, false);
                }
                else if (headState == 2)
                {
                    float speedDashNormal = isCacingKecil ? 21f : 17f;
                    if (npc.velocity != Vector2.Zero)
                    {
                        npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * speedDashNormal;
                    }

                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                    HandleHeadAttacks(npc, target, true);

                    float batas50Block = 50 * 16f;
                    if (jarakKePlayer >= batas50Block)
                    {
                        headState = 3;
                        retreatTimer = 90; // [BALANCE GUIDE] Durasi waktu menjauh setelah dash (90 ticks)
                        npc.netUpdate = true;
                    }
                }
                else if (headState == 3)
                {
                    retreatTimer--;
                    Vector2 arahMundur = npc.Center - target.Center;
                    if (arahMundur == Vector2.Zero) arahMundur = -Vector2.UnitY;
                    arahMundur.Normalize();

                    // [BALANCE GUIDE] Kecepatan mundur menjauhi player
                    float speedMundur = isCacingKecil ? 12f : 10f; 
                    npc.velocity = Vector2.Lerp(npc.velocity, arahMundur * speedMundur, 0.08f);
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    HandleHeadAttacks(npc, target, false);

                    if (retreatTimer <= 0)
                    {
                        headState = 0;
                        npc.netUpdate = true;
                    }
                }
            }

            // LOGIKA TEMBAKAN BAGIAN BADAN/EKOR
            if ((npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail) && !isOriginalImmunePart)
            {
                if (bodyShootTimer <= 0)
                {
                    bodyShootTimer = Main.rand.Next(300, 901); // [BALANCE GUIDE] Jeda acak menembak per segmen badan (ticks)
                }

                bodyShootTimer--;

                if (bodyShootTimer <= 0)
                {
                    bodyShootTimer = Main.rand.Next(300, 901);

                    float jarakCek = Vector2.Distance(npc.Center, target.Center);
                    if (jarakCek <= 850f && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --------------------------------------------------------------------------
                        // [BALANCE GUIDE: ATTACK - PROYEKTIL BADAN (CURSEDFLAME)]
                        // --------------------------------------------------------------------------
                        Vector2 arahKePlayer = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                        float speedPeluruBadan = 6.5f; // <--- SPEED tembakan proyektil badan
                        int damagePeluruBadan = 16;   // <--- DAMAGE tembakan proyektil badan

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            arahKePlayer * speedPeluruBadan,
                            ProjectileID.CursedFlameHostile,
                            damagePeluruBadan,
                            1f,
                            Main.myPlayer
                        );
                        // --------------------------------------------------------------------------
                    }
                }
            }
        }

        private void HandleHeadAttacks(NPC npc, Player target, bool forceEyeFire)
        {
            if (!forceEyeFire) return;

            if (eyeFireCooldown > 0) eyeFireCooldown--;
            if (eyeFireCooldown <= 0)
            {
                eyeFireCooldown = 8; // [BALANCE GUIDE] Jeda tembakan api beruntun dari kepala (semakin kecil semakin cepat)

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 arahMukaDepan = npc.velocity.SafeNormalize(Vector2.Zero);
                    if (arahMukaDepan == Vector2.Zero)
                    {
                        arahMukaDepan = (npc.rotation - MathHelper.PiOver2).ToRotationVector2();
                    }

                    // --------------------------------------------------------------------------
                    // [BALANCE GUIDE: ATTACK - EYEFIRE KEPALA]
                    // --------------------------------------------------------------------------
                    float speedEyeFire = 9.5f; // <--- SPEED semburan api EyeFire
                    int damageEyeFire = 18;   // <--- DAMAGE semburan api EyeFire

                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, arahMukaDepan * speedEyeFire, ProjectileID.EyeFire, damageEyeFire, 0.5f, Main.myPlayer);
                    // --------------------------------------------------------------------------
                }
            }

            if (babyEaterTimer == -1) { babyEaterTimer = Main.rand.Next(300, 450); }
            babyEaterTimer--;
            if (babyEaterTimer <= 0)
            {
                babyEaterTimer = Main.rand.Next(300, 450); // [BALANCE GUIDE] Jeda spawn minion BabyEaterWatcher
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // [BALANCE GUIDE] Jumlah minion yang di-spawn sekaligus (acak dari 2 sampai 3)
                    int jumlahBaby = Main.rand.Next(2, 4); 
                    for (int i = 0; i < jumlahBaby; i++)
                    {
                        Vector2 posisiSpawn = npc.Center + Main.rand.NextVector2Circular(30f, 30f);
                        Vector2 velSpit = Main.rand.NextVector2Unit() * 4f; // [BALANCE GUIDE] Kecepatan lempar awal minion
                        Projectile.NewProjectile(npc.GetSource_FromAI(), posisiSpawn, velSpit, ModContent.ProjectileType<BabyEaterWatcher>(), 0, 0f, Main.myPlayer);
                    }
                }
            }
        }

        private int HitungSisaSegmen(NPC head)
        {
            int total = 1;
            int indeksBerikutnya = (int)head.ai[1];
            while (indeksBerikutnya >= 0 && indeksBerikutnya < Main.maxNPCs)
            {
                NPC sekatTubuh = Main.npc[indeksBerikutnya];
                if (!sekatTubuh.active || (sekatTubuh.type != NPCID.EaterofWorldsBody && sekatTubuh.type != NPCID.EaterofWorldsTail))
                    break;
                total++;
                indeksBerikutnya = (int)sekatTubuh.ai[1];
                if (total > 80) break;
            }
            return total;
        }
    }
}