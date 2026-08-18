using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.Players;

namespace YourModName.Content.Items
{
    // ==========================================
    // OPTICAL WRENCH — staff pemanggil Twins-ally (TwinsAllySpazmatism +
    // TwinsAllyRetinazer, lihat OpticalWrenchPlayer.cs). Selama item ini yang
    // lagi dipegang/dipilih di hotbar, kedua ally otomatis muncul & selalu
    // di-refresh (lihat HoldItem).
    //
    // SPRITE: art di "OpticalWrench.png" (inventory) sengaja digambar MIRING
    // ke pojok kanan-atas "/" kayak sprite pedang biasa - UseStyle Swing
    // otomatis mengoreksinya jadi terlihat "berdiri tegak" ("|") pas dipegang
    // player (ini trik klasik semua sword-sprite vanilla, bukan bug).
    // "OpticalWrenchGlow.png" jadi glowmask, nyala full-bright pas item
    // dipegang di tangan / dijatuhin di lantai.
    // ==========================================
    public class OpticalWrench : ModItem
    {
        // Path sesuai lokasi sprite yang diminta:
        // TheSanity/Items/OpticWrench/OpticalWrench.png (+ OpticalWrenchGlow.png)
        public override string Texture => "TheSanity/Items/OpticWrench/OpticalWrench";

        public override void SetStaticDefaults()
        {
            // Glowmask otomatis dipakai tModLoader kalau nama file-nya
            // "<ItemName>_Glow"; berhubung filenya "OpticalWrenchGlow" (bukan
            // pola default), kita daftarkan manual lewat GlowMask di SetDefaults.
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 34;

            Item.damage = 75;
            Item.DamageType = DamageClass.Generic; // Class Less
            Item.crit = 35;                        // +35% crit chance

            // "Nail Speed" - tier super cepat, lebih cepat dari tier vanilla
            // tercepat ("Insanely Fast" ~useTime 15-20) - dipatok 6 tick.
            Item.useTime = 6;
            Item.useAnimation = 6;
            Item.useStyle = ItemUseStyleID.Swing; // sprite diagonal -> keliatan tegak pas idle
            Item.autoReuse = true;
            Item.noMelee = false;
            Item.knockBack = 4f;

            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item71;

            Item.glowMask = -1; // manual draw glow lewat PostDrawInInventory/PostDrawInWorld di bawah
        }

        // ---- Glow di inventory (item slot) ----
        public override void PostUpdate()
        {
            // Nyalain cahaya kecil pas item ada di dunia (dropped) biar match
            // request "OpticalWrenchGlow buat item yang saat di Drop/di Pegang".
            Lighting.AddLight(Item.Center, 0.55f, 0.25f, 0.65f);
        }

        public override bool PreDrawInWorld(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return true; // sprite dasar tetep digambar normal oleh vanilla
        }

        public override void PostDrawInWorld(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            DrawGlowOverlay(spriteBatch, Item.position - Main.screenPosition + new Vector2(Item.width / 2f, Item.height - Item.height / 2f), rotation, scale, Color.White);
        }

        public override void PostDrawInInventory(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Item inventory ("OpticalWrench.png") TIDAK pakai glow terpisah
            // (cuma di-drop/di-pegang sesuai request), jadi sengaja dikosongin.
        }

        private void DrawGlowOverlay(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Vector2 drawPos, float rotation, float scale, Color color)
        {
            var glowTexture = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>("TheSanity/Items/OpticWrench/OpticalWrenchGlow");
            if (glowTexture?.Value == null)
                return;

            Vector2 origin = glowTexture.Value.Size() * 0.5f;
            spriteBatch.Draw(glowTexture.Value, drawPos, null, color, rotation, origin, scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
        }

        // ==========================================
        // NOTE: held-in-hand drawing (glow overlay while item is equipped)
        // used to be done via PreDrawInHand, but that hook was removed from
        // ModItem in current tModLoader - held item drawing now goes through
        // ModItem.ModifyItemDraw(ref PlayerDrawSet, ref DrawData, ...) instead.
        // Since our old override just returned true (no actual custom draw),
        // it was safe to remove; add a ModifyItemDraw override here later if
        // an in-hand glow effect is actually needed.
        // ==========================================
        // SPAWN/KEEP-ALIVE TWINS-ALLY — selama item ini yang lagi dipegang
        // (selected di hotbar), Spaz-ally & Ret-ally selalu ada & timeLeft-nya
        // di-refresh terus tiap tick. Mekanik mana/regen/defense/backlash
        // lainnya ada di TwinsStaffPlayer.PostUpdateEquips (dipisah biar rapi).
        // ==========================================
        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return; // cukup client pemilik yang minta spawn - proyektil player-owned otomatis ke-sync

            float multiplier = player.GetModPlayer<TwinsStaffPlayer>().CurrentDamageMultiplier(player);
            int baseDamage = (int)(player.GetWeaponDamage(Item) * multiplier);

            bool hasSpaz = false;
            bool hasRet = false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI)
                    continue;

                if (p.type == ModContent.ProjectileType<YourModName.Content.NPCs.TwinsAllySpazmatism>())
                {
                    hasSpaz = true;
                    p.timeLeft = 2;
                    p.damage = baseDamage;
                }
                else if (p.type == ModContent.ProjectileType<YourModName.Content.NPCs.TwinsAllyRetinazer>())
                {
                    hasRet = true;
                    p.timeLeft = 2;
                    p.damage = baseDamage;
                }
            }

            if (!hasSpaz)
            {
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<YourModName.Content.NPCs.TwinsAllySpazmatism>(), baseDamage, 2f, player.whoAmI);
            }

            if (!hasRet)
            {
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<YourModName.Content.NPCs.TwinsAllyRetinazer>(), baseDamage, 2f, player.whoAmI);
            }
        }
    }
}
