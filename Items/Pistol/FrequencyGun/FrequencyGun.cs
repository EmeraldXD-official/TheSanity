using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TheSanity.Items.OreBar.Ambarium;
using TheSanity.Items.BossDrop;

namespace TheSanity.Items.Pistol.FrequencyGun
{
public class FrequencyGun : ModItem
	{
		public override void SetDefaults()
		{
			// --- Statistik Magic Pistol ---
			Item.damage = 30;
			Item.DamageType = DamageClass.Magic; // Kelas Mage
			Item.width = 68;
			Item.height = 60;
			Item.useTime = 54;
			Item.useAnimation = 54;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 4f;
			Item.value = Item.buyPrice(0, 15, 0, 0);
			Item.rare = ItemRarityID.Pink;
			Item.UseSound = SoundID.Item122; // Suara elektronik/sihir
			Item.autoReuse = true;
			
			// --- Konfigurasi Sinyal Sihir ---
			Item.mana = 12;
			Item.shoot = ProjectileID.NebulaLaser; 
			Item.shootSpeed = 12f;
		}
                public override Vector2? HoldoutOffset()
        {
            return new Vector2(-10f, 5f);
        }
        public override void ModifyManaCost(Player player, ref float reduce, ref float mult)
		{
			// Mengecek apakah pemain memakai full set Meteor Armor (Helm, Suit/Baju, Leggings/Celana)
			bool wearingMeteorArmor = player.armor[0].type == ItemID.MeteorHelmet && 
									  player.armor[1].type == ItemID.MeteorSuit && 
									  player.armor[2].type == ItemID.MeteorLeggings;

			if (wearingMeteorArmor)
			{
				mult = 0f; // Mengubah penggunaan mana menjadi 0 jika set lengkap
			}
		}

		// Efek cahaya di sekitar pemain saat senjata dipegang
		public override void HoldItem(Player player)
		{
			Lighting.AddLight(player.Center, 0.4f, 0.2f, 0.6f);
		}

		// Menembakkan proyektil dari ujung laras senjata secara friendly dan menyebar
		public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			// Menggeser titik awal kemunculan proyektil ke ujung laras senjata (30 piksel ke depan)
			Vector2 muzzleOffset = Vector2.Normalize(velocity) * 46f;
			if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0))
			{
				position += muzzleOffset;
			}

			int numProjectiles = 3; // 3 jalur proyektil (pola gelombang WiFi)
			float spreadAngle = MathHelper.ToRadians(16); // Lebar sudut penyebaran

			for (int i = 0; i < numProjectiles; i++)
			{
				float rotation = MathHelper.Lerp(-spreadAngle, spreadAngle, (float)i / (numProjectiles - 1));
				Vector2 perturbedSpeed = velocity.RotatedBy(rotation);

				// Memanggil proyektil di posisi ujung senjata
				int pIndex = Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
				
				// Memaksa proyektil menjadi Friendly dan aman bagi pemain
				if (pIndex < Main.maxProjectiles)
				{
					Main.projectile[pIndex].friendly = true;
					Main.projectile[pIndex].hostile = false;
                    Main.projectile[pIndex].penetrate = 3;
                    Main.projectile[pIndex].usesLocalNPCImmunity = true;
					Main.projectile[pIndex].localNPCHitCooldown = -1;
				}
			}

			// Efek partikel debu elektrik tepat di ujung senjata
			for (int i = -2; i <= 2; i++)
			{
				Vector2 dustPos = position + Vector2.Normalize(velocity).RotatedBy(i * 0.3f) * 10f;
				Dust dust = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Normalize(velocity) * 4f, 100, Color.Cyan, 1.1f);
				dust.noGravity = true;
			}

			return false; // Mencegah proyektil default bertumpuk
		}
         public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.SpaceGun, 1)
                .AddIngredient(ItemID.MeteoriteBar, 12)
                .AddIngredient<AmbariumBar>(15)
                .AddIngredient<BrokenTv>(15)
                .AddTile(TileID.Anvils)
                .Register();
        }
	}
}