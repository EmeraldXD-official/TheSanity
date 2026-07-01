using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace TheSanity
{
    public class ReworkedWraith : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Sekarang murni hanya memodifikasi Wraith asli saja
            return entity.type == NPCID.Wraith;
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.Wraith)
            {
                npc.lifeMax *= 2;
                npc.life = npc.lifeMax;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (npc.type == NPCID.Wraith)
            {
                return false; // Matikan contact damage biasa agar tidak bentrok dengan mekanik Sanity
            }
            return base.CanHitPlayer(npc, target, ref cooldownSlot);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Wraith)
            {
                modifiers.SetMaxDamage(1); // Murni hanya menerima 1 damage per hit
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Wraith)
            {
                modifiers.SetMaxDamage(1); // Murni hanya menerima 1 damage per hit
            }
        }

        public override void AI(NPC npc)
        {
            if (npc.type == NPCID.Wraith)
            {
                npc.TargetClosest(true);
                Player playerTarget = Main.player[npc.target];

                // Jika player aktif, hidup, dan jaraknya nempel/masuk ke tubuh (<= 50f)
                if (playerTarget.active && !playerTarget.dead && Vector2.Distance(npc.Center, playerTarget.Center) <= 50f)
                {
                    // Ambil komponen SanityPlayer dari target player
                    SanityPlayer sanityPlayer = playerTarget.GetModPlayer<SanityPlayer>();
                    
                    // Tambahkan Sanity sebesar 20% sekaligus
                    sanityPlayer.SanityCurrent += 20f;

                    // Berikan efek debuff visual bawaan biar ada impact-nya saat nempel (Opsional)
                    playerTarget.AddBuff(BuffID.Darkness, 180); // Buta sesaat selama 3 detik

                    if (Main.netMode == NetmodeID.Server)
                    {
                        // Sinkronisasi data ke server jika bermain multiplayer
                        NetMessage.SendData(MessageID.SyncPlayer, -1, -1, null, playerTarget.whoAmI);
                    }

                    // Buat Wraith langsung Despawn/Lenyap instan tanpa drop barang
                    npc.active = false;
                    return;
                }
            }
        }
    }
}