using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Input;

namespace TheSanity
{
    public class SolarFlareReworked : ModPlayer
    {
        // Status mode Beetle (true = Offense, false = Defense)
        public bool beetleModeOffense = true;

        public override void PostUpdateEquips()
        {
            // Cek apakah pemain memakai 1 set Solar Flare Armor lengkap
            if (Player.armor[0].type == ItemID.SolarFlareHelmet &&
                Player.armor[1].type == ItemID.SolarFlareBreastplate &&
                Player.armor[2].type == ItemID.SolarFlareLeggings)
            {
                // 1. Hallowed Armor Set Bonus (Holy Protection)
                Player.onHitDodge = true;

                // 2. Beetle Armor Set Bonus (Memilih salah satu agar kumbang muncul & tidak bug)
                if (beetleModeOffense)
                {
                    Player.beetleOffense = true;
                }
                else
                {
                    Player.beetleDefense = true;
                }

                // 3. Chlorophyte Armor Set Bonus (Leaf Crystal)
                Player.AddBuff(BuffID.LeafCrystal, 2);

                // 4. Turtle Armor Set Bonus (Full Damage Reflection)
                Player.turtleArmor = true;

                // Tooltip dinamis mengikuti mode Beetle yang aktif
                string beetleText = beetleModeOffense ? "Beetle Might (Offensif)" : "Beetle Shell (Defensif)";
                Player.setBonus += $"\n+ Holy Protection (Hallowed)\n+ {beetleText} [ALT + RMB pada Baju untuk Toggle]\n+ Leaf Crystal (Chlorophyte)\n+ Full Damage Reflection (Turtle)";
            }
        }
    }

    // Class untuk mendeteksi ALT + Klik Kanan pada baju di Inventory
    public class SolarBreastplateToggle : GlobalItem
    {
        public override bool CanRightClick(Item item)
        {
            // Hanya aktif jika item adalah Solar Flare Breastplate dan tombol ALT ditekan
            if (item.type == ItemID.SolarFlareBreastplate)
            {
                bool isAltPressed = Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
                return isAltPressed;
            }
            return base.CanRightClick(item);
        }

        public override void RightClick(Item item, Player player)
        {
            if (item.type == ItemID.SolarFlareBreastplate)
            {
                var modPlayer = player.GetModPlayer<SolarFlareReworked>();
                
                // Toggle status mode
                modPlayer.beetleModeOffense = !modPlayer.beetleModeOffense;

                // Tampilkan pesan di chat bahwa mode berhasil diubah
                string modeName = modPlayer.beetleModeOffense ? "Offensif (Beetle Might)" : "Defensif (Beetle Shell)";
                Main.NewText($"[TheSanity] Beetle Mode diubah ke: {modeName}", 255, 230, 100);
            }
        }
    }
}