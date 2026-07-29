using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class ClasslessToolsAndThrowables : GlobalItem
    {
        // =========================================================================
        // FILTER ITEM: MENENTUKAN ITEM APA SAJA YANG AKAN DIUBAH MENJADI CLASSLESS
        // =========================================================================
        private bool IsTargetItem(Item item)
        {
            // 1. Cek semua tipe Tools otomatis (Semua Pickaxe, Axe, dan Hammer)
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0)
                return true;

            // 2. Cek Payung (Umbrella & Tragic Umbrella)
            if (item.type == ItemID.Umbrella || item.type == ItemID.TragicUmbrella)
                return true;

            // 3. Cek Sekop (Gravedigger's Shovel)
            if (item.type == ItemID.GravediggerShovel)
                return true;

            // 4. Cek Granat, Bom, dan Dinamit (Termasuk semua varian Sticky / Bouncy / Dirt)
            if (item.type == ItemID.Grenade || item.type == ItemID.BouncyGrenade || item.type == ItemID.StickyGrenade ||
                item.type == ItemID.Bomb || item.type == ItemID.BouncyBomb || item.type == ItemID.StickyBomb || item.type == ItemID.DirtBomb || item.type == ItemID.DirtStickyBomb || item.type == ItemID.ScarabBomb || item.type == ItemID.WetBomb || item.type == ItemID.LavaBomb || item.type == ItemID.HoneyBomb || item.type == ItemID.DryBomb ||
                item.type == ItemID.Dynamite || item.type == ItemID.BouncyDynamite || item.type == ItemID.StickyDynamite)
            {
                return true;
            }

            // 5. Cek Shuriken & Throwing Knife (Termasuk Poisoned Knife)
            if (item.type == ItemID.Shuriken || item.type == ItemID.ThrowingKnife || item.type == ItemID.PoisonedKnife)
                return true;

            return false;
        }

        // =========================================================================
        // REWORK TOOLTIP: MENGUBAH "X MELEE DAMAGE" MENJADI "X DAMAGE"
        // =========================================================================
        public override void SetDefaults(Item item)
        {
            if (IsTargetItem(item))
            {
                // -------------------------------------------------------------------------
                // PANDUAN VISUAL:
                // Mengubah DamageType ke 'DamageClass.Default' akan menghapus label teks class 
                // pada item di Terraria secara otomatis (Menjadi murni "X Damage").
                // -------------------------------------------------------------------------
                item.DamageType = DamageClass.Default;
            }
        }

        // =========================================================================
        // REWORK SCALING: MENGADOPSI STAT MODIFIER DARI CLASS TERTINGGI PLAYER
        // =========================================================================
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (IsTargetItem(item))
            {
                // -------------------------------------------------------------------------
                // LOKASI GELEDAH STATS: Mengambil data bonus damage dari 4 Class utama
                // -------------------------------------------------------------------------
                StatModifier melee = player.GetTotalDamage(DamageClass.Melee);
                StatModifier ranged = player.GetTotalDamage(DamageClass.Ranged);
                StatModifier magic = player.GetTotalDamage(DamageClass.Magic);
                StatModifier summon = player.GetTotalDamage(DamageClass.Summon);

                // Mengonversi multiplier menjadi angka float murni untuk dibandingkan (Contoh: +20% damage -> 1.2f)
                float meleeVal = melee.ApplyTo(1f);
                float rangedVal = ranged.ApplyTo(1f);
                float magicVal = magic.ApplyTo(1f);
                float summonVal = summon.ApplyTo(1f);

                // -------------------------------------------------------------------------
                // LOGIKA PENENTUAN: Mencari nilai modifikasi tertinggi yang dimiliki player
                // -------------------------------------------------------------------------
                StatModifier highestModifier = melee;
                float maxDamageValue = meleeVal;

                if (rangedVal > maxDamageValue)
                {
                    maxDamageValue = rangedVal;
                    highestModifier = ranged;
                }
                if (magicVal > maxDamageValue)
                {
                    maxDamageValue = magicVal;
                    highestModifier = magic;
                }
                if (summonVal > maxDamageValue)
                {
                    maxDamageValue = summonVal;
                    highestModifier = summon;
                }

                // -------------------------------------------------------------------------
                // EKSEKUSI BALANCING:
                // Memaksa kalkulasi akhir damage item ini disamakan dengan stat class tertinggi.
                // -------------------------------------------------------------------------
                damage = highestModifier;
            }
        }
    }
}