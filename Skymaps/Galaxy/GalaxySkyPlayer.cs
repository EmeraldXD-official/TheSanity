using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Trigger untuk testing: background GalaxySky aktif selama player memakai Iron Helmet.
    ///
    /// Kalau nanti mau dipindah ke item modmu sendiri, tinggal ganti kondisi
    /// "ItemID.IronHelmet" di bawah menjadi ModContent.ItemType&lt;YourCustomHelmet&gt;().
    /// </summary>
    public class GalaxySkyPlayer : ModPlayer
    {
        private const string SkyKey = "TheSanity:GalaxySky";
        private const string ShaderKey = "TheSanity:GalaxySwirl";

        public override void PostUpdateEquips()
        {
            // Jaga-jaga: kalau karena suatu sebab sky belum terdaftar (misal timing Load()),
            // daftarkan di sini juga supaya tidak NullReferenceException.
            if (SkyManager.Instance[SkyKey] == null)
            {
                SkyManager.Instance[SkyKey] = new GalaxySky();
            }

            bool wearingIronHelmet = Player.armor[0].type == ItemID.IronHelmet;
            bool skyIsActive = SkyManager.Instance[SkyKey].IsActive();

            if (wearingIronHelmet && !skyIsActive)
            {
                SkyManager.Instance.Activate(SkyKey, Player.Center);
                Filters.Scene.Activate(ShaderKey);
            }
            else if (!wearingIronHelmet && skyIsActive)
            {
                SkyManager.Instance.Deactivate(SkyKey);
                if (Filters.Scene[ShaderKey] != null)
                    Filters.Scene[ShaderKey].Deactivate();
            }
        }
    }
}
