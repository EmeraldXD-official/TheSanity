using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class DrManFlyTransformation : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DrManFly;
        }

        private int humanFormTimer = 0;
        private int flyFormDuration = 0;
        public bool isTransformed = false;

        public override bool PreAI(NPC npc)
        {
            if (isTransformed)
            {
                npc.velocity = Vector2.Zero;
                npc.dontTakeDamage = true;
                npc.alpha = 255;       
                npc.chaseable = false; 

                flyFormDuration--;

                int aliveFlies = 0;
                int lastSurvivingFlyIndex = -1;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC other = Main.npc[i];
                    if (other.active && other.type == ModContent.NPCType<Fly>())
                    {
                        if (other.ModNPC is Fly flyMod && flyMod.masterWhoAmI == npc.whoAmI)
                        {
                            aliveFlies++;
                            lastSurvivingFlyIndex = i;
                        }
                    }
                }

                if (aliveFlies <= 0)
                {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false; 
                    return false;
                }

                if (flyFormDuration <= 0)
                {
                    isTransformed = false;
                    npc.dontTakeDamage = false;
                    npc.alpha = 0; 
                    npc.chaseable = true;
                    humanFormTimer = 0; 

                    if (lastSurvivingFlyIndex != -1)
                    {
                        npc.position = Main.npc[lastSurvivingFlyIndex].position;
                    }

                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC other = Main.npc[i];
                        if (other.active && other.type == ModContent.NPCType<Fly>())
                        {
                            if (other.ModNPC is Fly flyMod && flyMod.masterWhoAmI == npc.whoAmI)
                            {
                                other.active = false;
                            }
                        }
                    }
                }

                return false; 
            }

            return true;
        }

        public override void PostAI(NPC npc)
        {
            if (isTransformed) return;

            // [GUIDE LOCATION 3: HUMAN FORM COOLDOWN TO TRANSFORM]
            humanFormTimer++;
            if (humanFormTimer >= 420)
            {
                isTransformed = true;

                // [GUIDE LOCATION 4: FLY FORM DURATION (5 - 10 SECONDS)]
                flyFormDuration = Main.rand.Next(300, 601);

                int swarmCount = 10; 
                int flyDamage = 6;    

                for (int i = 0; i < swarmCount; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(35f, 35f);
                    int flyIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)(npc.Center.X + spawnOffset.X), (int)(npc.Center.Y + spawnOffset.Y), ModContent.NPCType<Fly>());

                    if (flyIndex < Main.maxNPCs)
                    {
                        NPC fly = Main.npc[flyIndex];
                        
                        // =====================================================================
                        // [GUIDE LOCATION 5: INSTANT HEALTH INJECTION ON SPAWN]
                        // Menyuntikkan nilai HP asli Dr. Man Fly saat ini ke tubuh lalat instan
                        // sebelum sistem AI lalat sempat berjalan mengecek perbandingan darah.
                        // =====================================================================
                        fly.lifeMax = npc.lifeMax;
                        fly.life = npc.life;

                        if (fly.ModNPC is Fly flyMod)
                        {
                            flyMod.masterWhoAmI = npc.whoAmI; 
                            fly.damage = flyDamage;
                            
                            if (Main.expertMode) fly.damage = (int)(flyDamage * 0.6f);
                        }
                    }
                }
            }
        }
    }
}