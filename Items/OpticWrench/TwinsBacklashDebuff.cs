using Terraria;
using Terraria.ModLoader;

namespace YourModName.Content.Buffs
{
    // ==========================================
    // Dipasang ke player lewat TwinsStaffPlayer.PostUpdateEquips saat player
    // melepas OpticalWrench SAAT Twins-ally masih aktif menghit/mengejar
    // target - efek -50% damage-nya sendiri diterapkan di
    // TwinsStaffPlayer.ModifyWeaponDamage (buff ini murni "penanda").
    // ==========================================
    public class TwinsBacklashDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + Terraria.ID.BuffID.Weak; // ganti ke sprite sendiri kalau sudah ada

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = false;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // Efek damage-nya dihandle di TwinsStaffPlayer.ModifyWeaponDamage
            // selama buff ini aktif (player.HasBuff check) - di sini gak perlu apa-apa lagi.
        }
    }
}
