using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Buffs;
using TheSanity.Items.ArmorBoss.Empress.Projectiles;

namespace TheSanity.Items.ArmorBoss.Empress.GlobalNPCs
{
    public class EmpressGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        private const int BoltCount = 4;
        private const float SeekRangeInTiles = 20;
        private const float BoltBaseDamage = 18f;

        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return; // spawning is server/singleplayer authoritative
            }

            int buffType = ModContent.BuffType<LightInYourSoulBuff>();
            if (npc.FindBuffIndex(buffType) < 0)
            {
                return;
            }

            List<NPC> nearbyTargets = FindNearbyEnemies(npc, SeekRangeInTiles * 16f, BoltCount);

            for (int i = 0; i < BoltCount; i++)
            {
                Vector2 direction = Main.rand.NextVector2Unit();
                int targetWhoAmI = -1;

                if (i < nearbyTargets.Count)
                {
                    targetWhoAmI = nearbyTargets[i].whoAmI;
                    direction = (nearbyTargets[i].Center - npc.Center).SafeNormalize(Vector2.UnitY);
                }

                Vector2 velocity = direction * 10f;

                // Damage here is a flat approximation — for exact player-summon-damage
                // scaling you'd want to route this through the killing player instead.
                int damage = (int)BoltBaseDamage;

                Projectile.NewProjectile(
                    npc.GetSource_Death(),
                    npc.Center,
                    velocity,
                    ModContent.ProjectileType<NightglowBolt>(),
                    damage,
                    1f,
                    Main.myPlayer,
                    ai0: targetWhoAmI
                );
            }
        }

        private List<NPC> FindNearbyEnemies(NPC excluding, float maxDistance, int maxCount)
        {
            var results = new List<NPC>();
            float maxDistSq = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.whoAmI == excluding.whoAmI)
                {
                    continue;
                }

                float distSq = Vector2.DistanceSquared(excluding.Center, npc.Center);
                if (distSq <= maxDistSq)
                {
                    results.Add(npc);
                }

                if (results.Count >= maxCount)
                {
                    break;
                }
            }

            return results;
        }
    }
}