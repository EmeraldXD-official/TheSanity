using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;
using Terraria.GameContent.ItemDropRules;

namespace TheSanity.GlobalNPC.Enemy
{
    public class CreeperEnemy : ModNPC
    {
        // =======================================================
        // STATE MACHINE
        // =======================================================
        private enum CreeperState
        {
            Idle,   // Ngejar / diam normal
            Fusing  // Sudah "nyala", siap meledak
        }

        private CreeperState state = CreeperState.Idle;

        // =======================================================
        // KONSTANTA MEKANIK (SESUAIKAN DI SINI KALAU MAU DI-TUNING)
        // =======================================================
        private const float DetectRadiusTiles = 7f;       // Radius deteksi: 7 block
        private const int MinBlockingTilesToBlock = 3;   // Minimal 3 block solid buat "membutakan" creeper
        private const int FuseDurationTicks = 60;         // 1 detik * 60 tick/detik
        private const int PointOfNoReturnTicks = 18;      // 0.3 detik terakhir = wajib meledak, ga bisa batal
        private const float NormalScale = 1f;
        private const float SwellScale = 1.25f;           // Seberapa besar dia "mengembang"
        private const int MinExplosionRadiusTiles = 5;
        private const int MaxExplosionRadiusTiles = 7;     // inklusif (dipakai dengan +1 di Main.rand.Next)
        private const float WaterRadiusReduction = 0.2f;   // Radius dikurangi 20% kalau creeper lagi di air

        // Damage ledakan (ke player & NPC musuh), beda-beda per difficulty
        private const int ExplosionDamageNormal = 100;
        private const int ExplosionDamageExpert = 200;
        private const int ExplosionDamageMaster = 300;

        // Damage proyektil DD2GoblinBomb yang dimuncratkan pas Hardmode, beda-beda per difficulty
        private const int GoblinBombDamageNormal = 35;
        private const int GoblinBombDamageExpert = 70;
        private const int GoblinBombDamageMaster = 100;

        private const int MinGoblinBombCount = 3;
        private const int MaxGoblinBombCount = 5;           // inklusif (dipakai dengan +1 di Main.rand.Next)

        // =======================================================
        // PROGRESSION: batas pickaxe power block yang bisa dihancurin ledakan
        // (0 = pra-hardmode gini, kunci berdasarkan boss yang udah dikalahin)
        // =======================================================
        private const int MaxPickPowerBeforeSkeletron = 65;
        private const int MaxPickPowerBeforePlantera = 100;
        private const int MaxPickPowerBeforeGolem = 210;
        private const int MaxPickPowerAfterMoonlord = 255;

        private const int AfterimageMaxCount = 4;          // Berapa banyak afterimage yang ditampilkan sekaligus
        private const int AfterimageSampleIntervalTicks = 4; // Rekam posisi baru tiap berapa tick

        // =======================================================
        // SOUND DEFINITIONS (path: TheSanity/Sounds/Creepers/...)
        // =======================================================
        private static readonly SoundStyle[] FuseSounds =
        {
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_fuse"),
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_fuse1"),
        };

        private static readonly SoundStyle[] HurtSounds =
        {
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_hurt1"),
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_hurt2"),
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_hurt3"),
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_hurt4"),
        };

        private static readonly SoundStyle[] ExplosionSounds =
        {
            new SoundStyle("TheSanity/Sounds/Creepers/Explosion1"),
            new SoundStyle("TheSanity/Sounds/Creepers/Explosion2"),
            new SoundStyle("TheSanity/Sounds/Creepers/Explosion3"),
            new SoundStyle("TheSanity/Sounds/Creepers/Explosion4"),
        };

        private static readonly SoundStyle DeathSoundStyle =
            new SoundStyle("TheSanity/Sounds/Creepers/Creeper_death");

        // =======================================================
        // FIELD RUNTIME
        // =======================================================
        private int fuseTimer = 0;

        // Riwayat posisi buat afterimage hijau selama fase fuse
        private List<Vector2> afterimagePositions = new List<Vector2>();
        private int afterimageSampleTimer = 0;

        // Item dye hijau, di-cache sekali biar ga bikin objek baru tiap frame
        private Item greenDyeItem;

        // Dummy texture bawaan agar tModLoader tidak mencari file .png baru
        public override string Texture => "Terraria/Images/Item_0";

        // Objek Dummy Player untuk menangani rendering armor secara otomatis
        private Player dummyPlayer;

        // =======================================================
        // SPAWNING:
        // - Surface: cuma malam hari, pool sama kayak Zombie, rate ~1/20 dibanding zombie biasa
        // - Underground / Cavern: kapan pun (siang/malam), rate dibikin dikit lebih tinggi (~1/14)
        // =======================================================
        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            Player player = spawnInfo.Player;

            // Underground (di antara permukaan & Cavern) ATAU Cavern -> boleh spawn kapan pun
            if (player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight)
            {
                // SpawnCondition.Underground = pool spawn umum di layer Underground/Cavern (ga terikat waktu).
                // Chance-nya dibikin dikit lebih tinggi (0.07 ~ 1/14) daripada versi Surface,
                // biar berasa "lebih edge" pas lagi eksplor gua/underground.
                return SpawnCondition.Underground.Chance * 0.07f;
            }

            // Surface -> cuma malam hari (pool sama kayak Zombie normal)
            if (player.ZoneOverworldHeight)
            {
                // Chance di sini itu BOBOT RELATIF dibanding entry lain di pool yang sama, bukan persentase absolut.
                // Zombie vanilla punya bobot dasar 1x dari base chance pool ini, jadi dikali 0.05 (1/20)
                // bikin creeper ini muncul kira-kira 1 dari 20 kali dibanding zombie biasa muncul.
                return SpawnCondition.OverworldNightMonster.Chance * 0.05f;
            }

            return 0f;
        }

        // Imun total ke damage dari proyektil ledakan (Bomb, Dynamite, Grenade, StickyBomb, Rocket, Mine, dll).
        // ProjectileID.Sets.Explosive udah nyakup hampir semua proyektil "berbau ledakan" bawaan vanilla,
        // termasuk yang modded kalau developernya nandain proyektilnya sebagai explosive juga.
        // Note: ini gak ngaruh ke ledakan si Creeper sendiri, soalnya damage ledakannya dikirim manual
        // lewat Player.Hurt()/NPC.SimpleStrikeNPC(), bukan lewat Projectile — jadi ga bakal ke-block di sini.
        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (ProjectileID.Sets.Explosive[projectile.type])
                return false;

            return base.CanBeHitByProjectile(projectile);
        }

        public override void SetStaticDefaults()
        {
            // Set 20 frame agar cocok dengan animasi berjalan humanoid/player di Terraria
            Main.npcFrameCount[Type] = 20;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new()
            {
                Velocity = 1f
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }

        public override void SetDefaults()
        {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 1; // Contact damage sengaja kecil — bahaya utama creeper ini ada di LEDAKANNYA, bukan sentuhan
            NPC.defense = 10;
            NPC.lifeMax = 120;

            // Hit sound dimatikan, digantikan random Creeper_hurt1-4 lewat HitEffect()
            NPC.HitSound = null;
            NPC.DeathSound = DeathSoundStyle;

            NPC.value = Item.buyPrice(silver: 50);
            NPC.knockBackResist = 0.5f;

            // Menggunakan Fighter AI (Perilaku AI Zombie / Humanoid) untuk gerakan chase dasar.
            // SENGAJA TIDAK pakai AIType = NPCID.Zombie: AIType bukan cuma nge-link gerakan,
            // tapi bikin game motret SELURUH kode AI vanilla Zombie apa adanya — termasuk suara
            // erangan idle acak (SoundID.Zombie, ambient) yang ke-trigger otomatis dan ga bisa
            // dimatikan dari sini. Karena semua logic (deteksi, fuse, explode) udah kita handle
            // manual lewat PostAI(), aiStyle=3 doang udah cukup buat gerakan chase/attack dasarnya.
            NPC.aiStyle = 3;

            // Tidak memancarkan cahaya saat Idle (biar "gelap" dulu kayak Creeper biasa).
            // Cahaya hijau baru nyala pas masuk state Fusing — lihat HandleFusingState().

            // Inisialisasi dimensi frame awal
            NPC.frame = new Rectangle(0, 0, 40, 56);
        }

        // =======================================================
        // LOOT TABLE
        // - ExplosivePowder: 100% chance, quantity 1-5
        // - CreeperMask / CreeperShirt / CreeperPants: cuma SALAH SATU yang bisa drop,
        //   dan total chance ketiganya digabung cuma 1% (OneFromOptions... = pilih 1 opsi
        //   secara acak dari daftar, lalu roll chance-nya SEKALI buat opsi yang kepilih itu,
        //   bukan tiap item di-roll sendiri-sendiri).
        // =======================================================
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // NOTE: CreeperMask / CreeperShirt / CreeperPants itu item VANILLA -> pakai ItemID.

            // ExplosivePowder itu item VANILLA -> pakai ItemID, bukan ModContent.ItemType<T>()
            npcLoot.Add(ItemDropRule.Common(
                ItemID.ExplosivePowder,
                chanceDenominator: 1,   // 1/1 = 100%
                minimumDropped: 1,
                maximumDropped: 5));

            npcLoot.Add(ItemDropRule.OneFromOptionsNotScalingWithLuck(
                chanceDenominator: 100, // 1/100 = 1% total, digabung buat ketiga item
                ItemID.CreeperMask,
                ItemID.CreeperShirt,
                ItemID.CreeperPants));
        }

        // =======================================================
        // MEKANIK UTAMA: DETEKSI, FUSE, LEDAKAN
        // Dipanggil SETELAH AI bawaan (fighter/zombie) jalan setiap tick,
        // jadi gerakan chase normal tetap berfungsi, kita cuma "menumpuk" logika di atasnya.
        // =======================================================
        public override void PostAI()
        {
            Player player = GetValidTarget();
            bool playerDetected = player != null && IsPlayerDetected(player);

            switch (state)
            {
                case CreeperState.Idle:
                    HandleIdleState(playerDetected);
                    break;

                case CreeperState.Fusing:
                    HandleFusingState(playerDetected);
                    break;
            }
        }

        private Player GetValidTarget()
        {
            Player player = Main.player[NPC.target];
            if (!player.active || player.dead)
            {
                NPC.TargetClosest(false);
                player = Main.player[NPC.target];
            }

            return (player.active && !player.dead) ? player : null;
        }

        private bool IsPlayerDetected(Player player)
        {
            float detectRadiusPixels = DetectRadiusTiles * 16f;
            float distance = Vector2.Distance(NPC.Center, player.Center);

            if (distance > detectRadiusPixels)
                return false;

            int blockingTiles = CountBlockingTilesBetween(player);
            return blockingTiles < MinBlockingTilesToBlock;
        }

        // Hitung berapa banyak tile solid UNIK yang menghalangi LOS antara creeper & player.
        // PENTING: nyoba BEBERAPA "ray" yang di-offset TEGAK LURUS terhadap arah pandang (bukan
        // selalu vertikal), BUKAN cuma satu garis lurus dari center-ke-center. Ini biar generalize
        // buat segala posisi:
        //  - Creeper di SAMPING player -> arah pandangnya horizontal -> offset tegak lurusnya VERTIKAL
        //    -> nangkep tembok yang TINGGI (ditumpuk ke atas).
        //  - Creeper di ATAS/BAWAH player -> arah pandangnya vertikal -> offset tegak lurusnya HORIZONTAL
        //    -> nangkep atap/lantai yang LEBAR (disusun ke samping).
        // Kalau offset-nya selalu vertikal (kayak sebelumnya), creeper yang di atas/bawah bakal terus-terusan
        // nyampling row tile yang SAMA doang (atap/lantai biasanya cuma 1 lapis tebal), makanya ga pernah
        // nyampe 3 tile unik walaupun keliatannya udah "dikurung" penuh.
        private int CountBlockingTilesBetween(Player player)
        {
            HashSet<Point> countedTiles = new HashSet<Point>();

            Vector2 toPlayer = player.Center - NPC.Center;
            if (toPlayer.LengthSquared() < 1f)
                toPlayer = Vector2.UnitY; // Safety kalau posisinya numpuk persis

            Vector2 direction = Vector2.Normalize(toPlayer);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X); // Tegak lurus terhadap arah pandang

            // Lebar sebaran ray, kira-kira selebar badan creeper/player
            float scanHalfWidth = Math.Max(NPC.width, player.width) * 0.5f;

            // -1.3/1.3 = sedikit ngelewatin tepi badan, sisanya nyebar dalem rentang badan
            float[] offsetFractions = { -1.3f, -0.8f, -0.4f, 0f, 0.4f, 0.8f, 1.3f };

            foreach (float frac in offsetFractions)
            {
                Vector2 offset = perpendicular * (scanHalfWidth * frac);
                Vector2 start = NPC.Center + offset;
                Vector2 end = player.Center + offset;

                SampleBlockingTilesAlongLine(start, end, countedTiles);

                // Optimisasi: begitu udah nyampe ambang batas, ga perlu cek ray sisanya
                if (countedTiles.Count >= MinBlockingTilesToBlock)
                    break;
            }

            return countedTiles.Count;
        }

        // Sampling satu garis lurus, tambahin tile solid unik yang ketemu ke countedTiles (shared antar ray).
        private void SampleBlockingTilesAlongLine(Vector2 start, Vector2 end, HashSet<Point> countedTiles)
        {
            float distance = Vector2.Distance(start, end);

            // Sampling tiap 4px biar ga ada celah tipis yang kelewat
            int steps = Math.Max(1, (int)(distance / 4f));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 samplePos = Vector2.Lerp(start, end, t);
                Point tileCoord = samplePos.ToTileCoordinates();

                if (countedTiles.Contains(tileCoord))
                    continue;

                if (!WorldGen.InWorld(tileCoord.X, tileCoord.Y))
                    continue;

                Tile tile = Main.tile[tileCoord.X, tileCoord.Y];
                if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    countedTiles.Add(tileCoord);

                    if (countedTiles.Count >= MinBlockingTilesToBlock)
                        return;
                }
            }
        }

        private void HandleIdleState(bool playerDetected)
        {
            // Badan kembali normal pelan-pelan
            NPC.scale = MathHelper.Lerp(NPC.scale, NormalScale, 0.15f);

            if (playerDetected)
            {
                StartFuse();
            }
        }

        private void StartFuse()
        {
            state = CreeperState.Fusing;
            fuseTimer = 0;
            afterimagePositions.Clear();
            afterimageSampleTimer = 0;

            SoundStyle fuseSound = FuseSounds[Main.rand.Next(FuseSounds.Length)];
            SoundEngine.PlaySound(fuseSound, NPC.Center);
        }

        private void HandleFusingState(bool playerDetected)
        {
            // Diam di tempat selama fuse (gravitasi tetap jalan, cuma gerak horizontal yang dikunci)
            NPC.velocity.X = 0f;

            // Mengembang
            NPC.scale = MathHelper.Lerp(NPC.scale, SwellScale, 0.15f);

            // Cahaya hijau nyata yang menerangi sekitar creeper, makin terang & makin "berdenyut"
            // cepat mendekati waktu meledak — dipanggil tiap tick (bukan di Draw) sesuai konvensi Lighting.AddLight.
            float fuseProgressForLight = fuseTimer / (float)FuseDurationTicks;
            float lightPulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * MathHelper.Lerp(0.3f, 0.9f, fuseProgressForLight));
            float lightIntensity = MathHelper.Lerp(0.6f, 1.8f, fuseProgressForLight) * lightPulse;
            Lighting.AddLight(NPC.Center, 0.1f * lightIntensity, 0.9f * lightIntensity, 0.15f * lightIntensity);

            // Rekam posisi buat afterimage hijau
            afterimageSampleTimer++;
            if (afterimageSampleTimer >= AfterimageSampleIntervalTicks)
            {
                afterimageSampleTimer = 0;
                afterimagePositions.Insert(0, NPC.Center);
                if (afterimagePositions.Count > AfterimageMaxCount)
                    afterimagePositions.RemoveAt(afterimagePositions.Count - 1);
            }

            fuseTimer++;

            // True kalau sisa waktu fuse <= 0.3 detik -> titik ga bisa balik lagi (tetap meledak)
            bool pastPointOfNoReturn = fuseTimer >= (FuseDurationTicks - PointOfNoReturnTicks);

            // Player kabur/LOS ketutup SEBELUM titik ga-bisa-balik -> batal, kembali normal
            if (!playerDetected && !pastPointOfNoReturn)
            {
                CancelFuse();
                return;
            }

            if (fuseTimer >= FuseDurationTicks)
            {
                Explode();
            }
        }

        private void CancelFuse()
        {
            state = CreeperState.Idle;
            fuseTimer = 0;
            afterimagePositions.Clear();
            afterimageSampleTimer = 0;
        }

        private int GetCurrentExplosionDamage()
        {
            if (Main.masterMode) return ExplosionDamageMaster;
            if (Main.expertMode) return ExplosionDamageExpert;
            return ExplosionDamageNormal;
        }

        private int GetCurrentGoblinBombDamage()
        {
            if (Main.masterMode) return GoblinBombDamageMaster;
            if (Main.expertMode) return GoblinBombDamageExpert;
            return GoblinBombDamageNormal;
        }

        // Muncratin 3-5 DD2GoblinBomb ke arah atas (dengan sebaran horizontal kecil) pas creeper meledak,
        // cuma terjadi kalau udah Hardmode.
        private void SpawnGoblinBombShrapnel()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return; // Projectile spawn cuma di server/singleplayer, nanti otomatis ke-sync ke client

            int bombCount = Main.rand.Next(MinGoblinBombCount, MaxGoblinBombCount + 1); // 3-5 inklusif
            int bombDamage = GetCurrentGoblinBombDamage();
            int maxPickPower = GetCurrentMaxPickaxePower();

            for (int i = 0; i < bombCount; i++)
            {
                // Sebaran horizontal kecil biar ga numpuk persis di satu titik, tapi arah utamanya tetap ke ATAS
                float velocityX = Main.rand.NextFloat(-3f, 3f);
                float velocityY = Main.rand.NextFloat(-9f, -6f); // Y negatif = ke atas

                int bombIndex = Projectile.NewProjectile(
                    NPC.GetSource_Death(),
                    NPC.Center,
                    new Vector2(velocityX, velocityY),
                    ProjectileID.DD2GoblinBomb,
                    bombDamage,
                    2f, // knockback
                    Main.myPlayer
                );
                Projectile bomb = Main.projectile[bombIndex];

                // DD2GoblinBomb punya sistem damage/tier internal sendiri dari event Old One's Army,
                // yang kadang nimpa/nge-inflate angka damage yang kita kasih di atas.
                // Global hook ini maksa ulang damage-nya PERSIS ke bombDamage, apapun yang terjadi di tengah jalan,
                // dan sekalian nge-handle ledakan tile radius 3 block-nya (lihat CreeperGoblinBombDamageLock.cs).
                CreeperGoblinBombDamageLock damageLock = bomb.GetGlobalProjectile<CreeperGoblinBombDamageLock>();
                damageLock.isFromCreeperExplosion = true;
                damageLock.lockedDamage = bombDamage;
                damageLock.maxPickPower = maxPickPower;
            }
        }

        // Batas pickaxe power block yang bisa dihancurin ledakan, sesuai progression boss yang udah dikalahin.
        private int GetCurrentMaxPickaxePower()
        {
            if (NPC.downedMoonlord) return MaxPickPowerAfterMoonlord;
            if (NPC.downedGolemBoss) return MaxPickPowerBeforeGolem; // belum ada tier baru yang diminta antara Golem-Moonlord
            if (NPC.downedPlantBoss) return MaxPickPowerBeforeGolem;
            if (NPC.downedBoss3) return MaxPickPowerBeforePlantera; // downedBoss3 = Skeletron
            return MaxPickPowerBeforeSkeletron;
        }

        private void Explode()
        {
            SoundStyle explosionSound = ExplosionSounds[Main.rand.Next(ExplosionSounds.Length)];
            SoundEngine.PlaySound(explosionSound, NPC.Center);

            int radiusTiles = Main.rand.Next(MinExplosionRadiusTiles, MaxExplosionRadiusTiles + 1); // 10-15 inklusif
            float radiusPixels = radiusTiles * 16f;

            // NPC.wet = lagi kena cairan apapun, dikombinasikan dengan !lavaWet & !honeyWet biar spesifik AIR aja
            bool inWater = NPC.wet && !NPC.lavaWet && !NPC.honeyWet;
            if (inWater)
            {
                radiusPixels *= (1f - WaterRadiusReduction); // reduce 20%
            }

            int explosionDamage = GetCurrentExplosionDamage();

            // Damage semua player dalam radius ledakan
            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead)
                    continue;

                float dist = Vector2.Distance(NPC.Center, p.Center);
                if (dist <= radiusPixels)
                {
                    // Falloff sederhana: makin deket makin sakit
                    float falloff = 1f - (dist / radiusPixels);
                    int damage = (int)(explosionDamage * MathHelper.Clamp(falloff, 0.3f, 1f));

                    Vector2 knockbackDir = (p.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

                    // Reset immunity dulu SEBELUM apply damage ledakan. Root cause: creeper diem di
                    // tempat selama fase Fusing (1 detik) dan sering masih nempel/nyentuh player,
                    // yang berarti player kena contact damage kecil (NPC.damage = 1) berkali-kali.
                    // Contact damage itu pakai immunity pool yang SAMA kayak Player.Hurt() manual di
                    // sini -> pas ledakan beneran kejadian, player kemungkinan besar masih dalam
                    // window immune dari "colekan" terakhir, jadi damage ledakan yang gede ke-block
                    // total. Reset paksa ini mastiin ledakan SELALU tembus.
                    p.immune = false;
                    p.immuneTime = 0;

                    p.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(), damage, 0);
                    p.velocity += knockbackDir * 8f;
                }
            }

            // Ledakan netral: NPC musuh lain dalam radius juga kena damage (bukan cuma player)
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                foreach (NPC otherNpc in Main.npc)
                {
                    // Skip diri sendiri, NPC yang ga aktif, dan NPC friendly (bukan musuh)
                    if (!otherNpc.active || otherNpc.friendly || otherNpc.whoAmI == NPC.whoAmI)
                        continue;

                    float npcDist = Vector2.Distance(NPC.Center, otherNpc.Center);
                    if (npcDist <= radiusPixels)
                    {
                        float npcFalloff = 1f - (npcDist / radiusPixels);
                        int npcDamage = (int)(explosionDamage * MathHelper.Clamp(npcFalloff, 0.3f, 1f));

                        int hitDirection = otherNpc.Center.X >= NPC.Center.X ? 1 : -1;
                        otherNpc.SimpleStrikeNPC(npcDamage, hitDirection, noPlayerInteraction: true, knockBack: 4f);
                    }
                }
            }

            // Hancurin block di radius ledakan — cuma di server/singleplayer biar ga desync di multiplayer
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                DestroyTilesInRadius(NPC.Center, radiusPixels);
            }

            // Kalau udah Hardmode, ledakan juga muncratin beberapa DD2GoblinBomb ke arah atas
            if (Main.hardMode)
            {
                SpawnGoblinBombShrapnel();
            }

            afterimagePositions.Clear();

            // Creeper "hilang", BUKAN "mati" — jadi ga lewat checkDead()/HitEffect(),
            // ga ada death sound/animasi mati, dan ga ada loot drop dari NPC itu sendiri.
            NPC.active = false;

            if (Main.netMode == NetmodeID.Server)
            {
                // Sinkronkan status non-aktif ini ke semua client biar NPC-nya ikut hilang di layar mereka
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        // Hancurkan semua tile solid dalam radius lingkaran di sekitar titik ledakan.
        // Tile yang butuh pickaxe power lebih tinggi dari yang "dikuasai" creeper (sesuai progression boss)
        // bakal di-skip sama sekali — persis kayak player belum punya pickaxe yang cukup kuat.
        private void DestroyTilesInRadius(Vector2 center, float radiusPixels)
        {
            CreeperExplosionUtils.DestroyTilesInRadius(center, radiusPixels, GetCurrentMaxPickaxePower());
        }

        // =======================================================
        // SUARA SAAT KENA DAMAGE (random Creeper_hurt1 - hurt4)
        // =======================================================
        public override void HitEffect(NPC.HitInfo hit)
        {
            if (NPC.life > 0)
            {
                SoundStyle hurtSound = HurtSounds[Main.rand.Next(HurtSounds.Length)];
                SoundEngine.PlaySound(hurtSound, NPC.Center);
            }
        }

        // =======================================================
        // ANIMASI FRAME HUMANOID (SYNC DENGAN PLAYER FRAME)
        // =======================================================
        public override void FindFrame(int frameHeight)
        {
            NPC.spriteDirection = NPC.direction;

            if (NPC.velocity.Y != 0)
            {
                // Frame Melompat / Melayang
                NPC.frame.Y = 5 * 56;
            }
            else if (NPC.velocity.X == 0)
            {
                // Frame Diam / Idle
                NPC.frame.Y = 0;
            }
            else
            {
                // Animasi Berjalan (Frame 6 s/d 19)
                NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;
                if (NPC.frameCounter >= 14)
                {
                    NPC.frameCounter = 0;
                }
                int currentWalkFrame = 6 + (int)NPC.frameCounter;
                NPC.frame.Y = currentWalkFrame * 56;
            }
        }

        // =======================================================
        // CUSTOM DRAW: DUMMY PLAYER RENDERER (PERFECT CREEPER ARMOR)
        // + AFTERIMAGE HIJAU SAAT FASE FUSE
        // =======================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // 1. Memaksa Terraria memuat aset armor ke memori
            Main.instance.LoadArmorHead(118); // Creeper Mask
            Main.instance.LoadArmorBody(79);  // Creeper Shirt
            Main.instance.LoadArmorLegs(67);  // Creeper Pants

            // 2. Inisialisasi Dummy Player dengan dimensi yang VALID (Mencegah DivideByZeroException)
            if (dummyPlayer == null)
            {
                dummyPlayer = new Player();
                dummyPlayer.width = 20;   // Ukuran standar player
                dummyPlayer.height = 42;  // Ukuran standar player
            }

            if (greenDyeItem == null)
            {
                greenDyeItem = new Item();
                greenDyeItem.SetDefaults(ItemID.GreenDye);
            }

            // 3. Setup dasar dummy (dipakai bareng buat afterimage & gambar utama)
            dummyPlayer.direction = NPC.direction;
            dummyPlayer.head = 118;
            dummyPlayer.body = 79;
            dummyPlayer.legs = 67;
            dummyPlayer.GetModPlayer<CreeperDummyPlayer>().HideHands = true;

            // 3b. Paksa dummyPlayer selalu dianggap "kering" & diam.
            // Root cause: dummyPlayer nggak pernah lewat Player.Update() penuh, jadi field kayak
            // .wet dan .velocity nggak sinkron dan bisa ke-baca "true"/nonzero secara nyasar oleh
            // sistem composite-arm renderer (dipakai buat pose renang/reaching), yang kadang bikin
            // sepotong lengan sempat kelihatan pas NPC lompat sambil nyentuh air. Reset manual ini
            // mencegah composite renderer masuk ke pose tersebut.
            dummyPlayer.wet = false;
            dummyPlayer.lavaWet = false;
            dummyPlayer.honeyWet = false;
            dummyPlayer.velocity = Vector2.Zero;
            dummyPlayer.gravDir = 1f;

            Rectangle currentFrame = new Rectangle(0, NPC.frame.Y, 40, 56);
            dummyPlayer.headFrame = currentFrame;
            dummyPlayer.bodyFrame = currentFrame;
            dummyPlayer.legFrame = currentFrame;

            // 4. Gambar outline hijau tebal berdenyut (di belakang afterimage & sprite utama)
            if (state == CreeperState.Fusing)
            {
                DrawGlowOutline();
            }

            // 5. Gambar afterimage hijau (dari yang paling lama ke yang paling baru)
            if (state == CreeperState.Fusing && afterimagePositions.Count > 0)
            {
                for (int i = afterimagePositions.Count - 1; i >= 0; i--)
                {
                    // i besar = posisi lama -> makin transparan. i kecil = posisi baru -> makin solid.
                    float ageFactor = (float)(i + 1) / (afterimagePositions.Count + 1);
                    float shadowAmount = MathHelper.Lerp(0.35f, 0.85f, ageFactor);

                    DrawAfterimage(afterimagePositions[i], shadowAmount);
                }
            }

            // 6. Bersihkan dye (biar gambar utama TIDAK ikut ke-tint hijau)
            dummyPlayer.dye[0] = new Item();
            dummyPlayer.dye[1] = new Item();
            dummyPlayer.dye[2] = new Item();

            // 7. Sinkronkan posisi utama & gambar player asli
            // HideHands di-reset ke false otomatis setiap habis DrawPlayer() (lihat CreeperDummyPlayer.HideDrawLayers),
            // jadi harus di-set true LAGI tepat sebelum panggilan DrawPlayer utama ini,
            // kalau nggak, tangan bakal nongol karena udah "ke-reset" duluan sama afterimage di atas.
            dummyPlayer.GetModPlayer<CreeperDummyPlayer>().HideHands = true;
            dummyPlayer.Center = NPC.Center;
            dummyPlayer.position.Y += NPC.gfxOffY;

            Main.PlayerRenderer.DrawPlayer(
                Main.Camera,
                dummyPlayer,
                dummyPlayer.position,
                NPC.rotation,
                dummyPlayer.fullRotationOrigin,
                0f,
                NPC.scale
            );

            return false;
        }

        // Gambar beberapa salinan sprite di-offset kecil ke 8 arah pakai dye hijau solid,
        // di belakang sprite utama & afterimage -> keliatan kayak "outline" tebal yang menyala.
        // Ketebalan & kecerahan berdenyut, makin intens mendekati waktu meledak (mirip Creeper Minecraft).
        private static readonly Vector2[] OutlineOffsetDirections =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
            new Vector2(-1,  0),                      new Vector2(1,  0),
            new Vector2(-1,  1), new Vector2(0,  1), new Vector2(1,  1),
        };

        private void DrawGlowOutline()
        {
            float fuseProgress = fuseTimer / (float)FuseDurationTicks;

            // Denyut makin cepat mendekati ledakan
            float pulseSpeed = MathHelper.Lerp(0.15f, 0.5f, fuseProgress);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GameUpdateCount * pulseSpeed);

            // Outline makin tebal mendekati ledakan (2px -> 4px)
            float thicknessPixels = MathHelper.Lerp(2f, 4f, fuseProgress) * pulse;

            Vector2 centerBackup = dummyPlayer.Center;

            dummyPlayer.dye[0] = greenDyeItem;
            dummyPlayer.dye[1] = greenDyeItem;
            dummyPlayer.dye[2] = greenDyeItem;

            foreach (Vector2 direction in OutlineOffsetDirections)
            {
                dummyPlayer.Center = centerBackup + direction * thicknessPixels;
                dummyPlayer.position.Y += NPC.gfxOffY;

                // HideHands ke-reset otomatis abis TIAP DrawPlayer() (lihat CreeperDummyPlayer.HideDrawLayers),
                // jadi harus di-set true ULANG di sini, di dalam loop — kalau di luar loop (sebelum foreach),
                // cuma copy PERTAMA dari 8 outline ini yang tangannya beneran ke-hide, 7 sisanya nongol lagi.
                dummyPlayer.GetModPlayer<CreeperDummyPlayer>().HideHands = true;

                Main.PlayerRenderer.DrawPlayer(
                    Main.Camera,
                    dummyPlayer,
                    dummyPlayer.position,
                    NPC.rotation,
                    dummyPlayer.fullRotationOrigin,
                    0f, // shadow 0 = gambar solid/opaque, bukan silhouette transparan
                    NPC.scale
                );
            }

            dummyPlayer.Center = centerBackup;
        }

        // Gambar satu "hantu" hijau di posisi lama.
        // shadowAmount: parameter bawaan DrawPlayer (0 = full opaque, mendekati 1 = makin transparan) —
        // ini mekanisme vanilla yang sama dipakai buat afterimage dash/mount, jadi ga perlu shader custom.
        private void DrawAfterimage(Vector2 worldCenter, float shadowAmount)
        {
            dummyPlayer.dye[0] = greenDyeItem;
            dummyPlayer.dye[1] = greenDyeItem;
            dummyPlayer.dye[2] = greenDyeItem;

            // HideHands ke-reset tiap habis DrawPlayer(), jadi harus di-set true lagi tiap afterimage
            dummyPlayer.GetModPlayer<CreeperDummyPlayer>().HideHands = true;

            dummyPlayer.Center = worldCenter;
            dummyPlayer.position.Y += NPC.gfxOffY;

            Main.PlayerRenderer.DrawPlayer(
                Main.Camera,
                dummyPlayer,
                dummyPlayer.position,
                NPC.rotation,
                dummyPlayer.fullRotationOrigin,
                shadowAmount,
                NPC.scale
            );
        }
    }
}
