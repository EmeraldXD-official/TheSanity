using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Armor.Ambarium
{
    // ==========================================
    // 1. HELM RANGER (AMBARIUM VISOR)
    // ==========================================
    [AutoloadEquip(EquipType.Head)]
    public class AmbariumVisor : ModItem
    {
        public override void SetStaticDefaults()
        {
            ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 4;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.04f;
            player.GetCritChance(DamageClass.Ranged) += 6f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<AmbariumChestplate>() &&
                   legs.type == ModContent.ItemType<AmbariumLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = Language.GetTextValue("Mods.TheSanity.Items.AmbariumVisor.SetBonus");
            player.GetModPlayer<AmbariumArmorPlayer>().rangerSet = true;
            player.ammoCost80 = true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
            .AddIngredient<AmbariumBar>(10)
            .AddIngredient(237)
            .AddTile(TileID.Anvils)
            .Register();
        }
    }

    // ==========================================
    // 2. HELM MAGIC (AMBARIUM HOOD)
    // ==========================================
    [AutoloadEquip(EquipType.Head)]
    public class AmbariumHood : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 3;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Magic) += 0.04f;
            player.statManaMax2 += 40;
            player.manaCost -= 0.06f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<AmbariumChestplate>() &&
                   legs.type == ModContent.ItemType<AmbariumLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = Language.GetTextValue("Mods.TheSanity.Items.AmbariumHood.SetBonus");
            player.GetModPlayer<AmbariumArmorPlayer>().magicSet = true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
            .AddIngredient<AmbariumBar>(10)
            .AddIngredient(5057)
            .AddTile(TileID.Anvils)
            .Register();
        }
    }

    // ==========================================
    // 3. HELM SUMMONER (AMBARIUM MASK)
    // ==========================================
    [AutoloadEquip(EquipType.Head)]
    public class AmbariumMask : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 2;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Summon) += 0.04f;
            player.maxMinions += 1;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<AmbariumChestplate>() &&
                   legs.type == ModContent.ItemType<AmbariumLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = Language.GetTextValue("Mods.TheSanity.Items.AmbariumMask.SetBonus");
            player.GetModPlayer<AmbariumArmorPlayer>().summonerSet = true;
            player.whipRangeMultiplier += 0.15f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
            .AddIngredient<AmbariumBar>(10)
            .AddIngredient(1135)
            .AddTile(TileID.Anvils)
            .Register();
        }
    }

    // ==========================================
    // 4. CHESTPLATE
    // ==========================================
    [AutoloadEquip(EquipType.Body)]
    public class AmbariumChestplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 6;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Generic) += 0.05f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
            .AddIngredient<AmbariumBar>(15)
            .AddIngredient(175,10)
            .AddTile(TileID.Anvils)
            .Register();
        }
    }

    // ==========================================
    // 5. LEGGINGS
    // ==========================================
    [AutoloadEquip(EquipType.Legs)]
    public class AmbariumLeggings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 0, 90, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 5;
        }

        public override void UpdateEquip(Player player)
        {
            player.moveSpeed += 0.10f;
            player.GetCritChance(DamageClass.Generic) += 3f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
            .AddIngredient<AmbariumBar>(12)
            .AddIngredient(175,8)
            .AddTile(TileID.Anvils)
            .Register();
        }
    }

    // ==========================================
    // 6. MODPLAYER UTAMA LOGIKA ARMOR
    // ==========================================
    public class AmbariumArmorPlayer : ModPlayer
    {
        public bool rangerSet;
        public bool magicSet;
        public bool summonerSet;

        public int rangerHits = 0;
        public int rangerActiveTimer = 0;   // ⏱️ Durasi Aktif (300 tick = 5 detik)
        public int rangerRechargeTimer = 0; // ⏱️ Recharge CD (600 tick = 10 detik)

        public int magicSpells = 0;

        public override void ResetEffects()
        {
            rangerSet = false;
            magicSet = false;
            summonerSet = false;
        }

        public override void PostUpdate()
        {
            // ⏱️ SIKLUS TIMER RANGER SET
            if (rangerActiveTimer > 0)
            {
                rangerActiveTimer--;
                if (rangerActiveTimer <= 0)
                {
                    // Durasi 5 detik habis -> Mulai Recharge 10 detik
                    rangerRechargeTimer = 600; 
                    RemoveTargetMarker();
                }
            }
            else if (rangerRechargeTimer > 0)
            {
                rangerRechargeTimer--;
            }

            // 🔮 SUMMONER: Spawn / Maintain Sentinel Pet
            if (summonerSet && Player.whoAmI == Main.myPlayer)
            {
                if (Player.ownedProjectileCounts[ModContent.ProjectileType<AmbariumSentinelProj>()] < 1)
                {
                    Projectile.NewProjectile(
                        Player.GetSource_Misc("AmbariumSetBonus"),
                        Player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<AmbariumSentinelProj>(),
                        35,
                        2f,
                        Player.whoAmI
                    );
                }
            }
        }

        // 🏹 RANGER: MENEMBAK BERSAMAAN DENGAN PLAYER SAAT BURST MODE (5 DETIK)
        public override bool Shoot(Item item, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Menembak sesuai Attack Speed Player SELAMA durasi 5 detik aktif!
            if (rangerSet && item.CountsAsClass(DamageClass.Ranged) && rangerActiveTimer > 0)
            {
                NPC markedTarget = FindMarkedTarget();
                if (markedTarget != null)
                {
                    Vector2 offset = new Vector2(Main.rand.NextBool() ? -65f : 65f, Main.rand.NextFloat(-30f, 10f));
                    Vector2 clonePos = markedTarget.Center + offset;
                    Vector2 shootVel = (markedTarget.Center - clonePos).SafeNormalize(Vector2.UnitX) * 16f;

                    Projectile.NewProjectile(
                        Player.GetSource_Misc("AmbariumRangerClone"),
                        clonePos,
                        shootVel,
                        ModContent.ProjectileType<AmbariumShadowCloneProj>(),
                        (int)(damage * 0.5f), // 50% damage per-peluru clone
                        knockback,
                        Player.whoAmI,
                        markedTarget.whoAmI
                    );
                }
            }
            return true;
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 🏹 RANGER: Charge Hit HANYA BISA DIISI jika tidak sedang Aktif & tidak sedang Recharge
            if (rangerSet && proj.CountsAsClass(DamageClass.Ranged) && rangerActiveTimer <= 0 && rangerRechargeTimer <= 0)
            {
                rangerHits++;
                if (rangerHits >= 8)
                {
                    rangerHits = 0;
                    NPC strongestNPC = FindStrongestTarget(800f);
                    if (strongestNPC != null)
                    {
                        SoundEngine.PlaySound(SoundID.Item43, strongestNPC.Center);

                        // 🔥 AKTIFKAN DURASI BURST MODE 5 DETIK (300 ticks)
                        rangerActiveTimer = 300;

                        // Spawn Marker di musuh terkuat selama 5 detik
                        Projectile.NewProjectile(
                            Player.GetSource_OnHit(target),
                            strongestNPC.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<AmbariumTargetMarker>(),
                            0,
                            0f,
                            Player.whoAmI,
                            strongestNPC.whoAmI
                        );
                    }
                }
            }

            // 🔮 SUMMONER: Isi Soul Gauge Sentinel
            if (summonerSet && (proj.minion || proj.CountsAsClass(DamageClass.Summon) || ProjectileID.Sets.IsAWhip[proj.type]))
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<AmbariumSentinelProj>())
                    {
                        p.ai[0] += 12f;
                        break;
                    }
                }
            }
        }

        // 🪄 MAGIC: Runic Supernova
        public override void OnConsumeMana(Item item, int manaConsumed)
        {
            if (magicSet && manaConsumed > 0)
            {
                magicSpells++;
                if (magicSpells >= 5)
                {
                    magicSpells = 0;
                    SoundEngine.PlaySound(SoundID.Item29, Main.MouseWorld);

                    Projectile.NewProjectile(
                        Player.GetSource_ItemUse(item),
                        Main.MouseWorld,
                        Vector2.Zero,
                        ModContent.ProjectileType<AmbariumSupernovaCore>(),
                        30,
                        3f,
                        Player.whoAmI
                    );
                }
            }
        }

        private void RemoveTargetMarker()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<AmbariumTargetMarker>())
                {
                    p.Kill();
                }
            }
        }

        private NPC FindMarkedTarget()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<AmbariumTargetMarker>())
                {
                    int npcIdx = (int)p.ai[0];
                    if (npcIdx >= 0 && npcIdx < Main.maxNPCs && Main.npc[npcIdx].active && !Main.npc[npcIdx].friendly)
                    {
                        return Main.npc[npcIdx];
                    }
                }
            }
            return null;
        }

        private NPC FindStrongestTarget(float range)
        {
            NPC strongest = null;
            int highestHP = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(Player) && Vector2.Distance(Player.Center, npc.Center) < range)
                {
                    if (npc.lifeMax > highestHP)
                    {
                        highestHP = npc.lifeMax;
                        strongest = npc;
                    }
                }
            }
            return strongest;
        }
    }
}