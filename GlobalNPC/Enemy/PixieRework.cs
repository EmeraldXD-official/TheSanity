using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class PixieRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Pixie;
        }

        public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
        {
            npc.localAI[0] = Main.rand.Next(0, 180);
        }

        public override void AI(NPC npc)
        {
            Player targetPlayer = Main.player[npc.target];
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead) return;

            npc.localAI[0]++;

            if (npc.localAI[0] >= 180)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float projectileSpeed = 7f; 
                    int projectileDamage = 20; 

                    Vector2 shootVelocity = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY) * projectileSpeed;

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocity,
                        ProjectileID.HallowBossRainbowStreak, 
                        projectileDamage,
                        1f,
                        Main.myPlayer
                    );
                }

                npc.localAI[0] = 0;
            }
        }

        // =========================================================================
        // PERBAIKAN UTAMA: MEMAKSA SUARA KUSTOM TERPICU SAAT TABRAKAN FISIK (CONTACT)
        // =========================================================================
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            // Menyiapkan audio dari folder: TheSanity/Sounds/heylisten.mp3
            SoundStyle heyListenSound = new SoundStyle("TheSanity/Sounds/heylisten")
            {
                Volume = 1.2f,        // Dinaikkan sedikit agar suaranya terdengar jelas saat rusuh
                PitchVariance = 0.1f  // Sedikit variasi nada biar tidak monoton
            };

            // Mainkan suara tepat di tubuh player yang ditabrak Pixie
            SoundEngine.PlaySound(heyListenSound, target.Center);
        }
    }
}