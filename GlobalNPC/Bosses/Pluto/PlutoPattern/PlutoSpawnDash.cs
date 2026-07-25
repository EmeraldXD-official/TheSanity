using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // Luminance ScreenShakeSystem

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================================
    // 🛑 [PATTERN 9 - SPAWN ANIMATION] Cutscene pembuka, SATU KALI doang seumur hidup NPC ini
    // (di-trigger dari blok `if (!initialized)` di PlutoHead.cs, BUKAN lewat pool gacha
    // NPC.ai[0]==0f -- makanya nomor pattern-nya sengaja "dibuang jauh" ke 9 biar ga numpuk sama
    // Pattern 1-8 yang emang isi pool acak).
    //
    // Alurnya (4 stage, reuse ai[1]=stage & ai[2]=timer, gaya sama kayak pattern lain):
    //   Stage 0 (CameraPullIn)  : layar player yang jadi target (NPC.target) ditarik ke ~50 block
    //                             DI ATAS dirinya sendiri. BARENGAN itu Pluto di-teleport ke titik
    //                             jauh di luar layar, salah satu dari 3 arah acak (atas / kiri /
    //                             kanan -- SESUAI REQUEST, ga pernah dari bawah).
    //   Stage 1 (RunIn)         : Pluto "berlari" (dash) CEPAT dari titik spawn tadi menuju titik
    //                             kedatangan di depan/atas player.
    //   Stage 2 (RoarHold)      : Pluto berhenti mendadak, ngeluarin suara Roar + screen shake,
    //                             DAN di titik inilah HasTriggeredBackgroundReveal dinyalain
    //                             (lihat PlutoBackgroundSystem.cs -- baru dari sini background
    //                             boss beneran "pop" muncul, bukan dari awal NPC ke-spawn).
    //   Stage 3 (CameraReturn)  : Layar player pelan-pelan ditarik balik ke posisi normal.
    // Begitu Stage 3 kelar, pattern LANGSUNG nembak Pattern 1 (Normal Dash) tanpa lewat gacha
    // picker (NPC.ai[0]==0f), persis kayak instruksi user.
    //
    // Invincibility Head/Body/Tail selama SELURUH pattern ini sudah di-handle terpisah:
    // - Head       : PlutoHead.cs, kondisi `NPC.dontTakeDamage` di AI() utama.
    // - Body/Tail  : PlutoBody.cs, `isTeleportInvinciblePhase`.
    //
    // 🎥 [KAMERA] Ditarik lewat ModPlayer.ModifyScreenPosition() -- lihat PlutoSpawnCameraPlayer.cs.
    // Field `SpawnAnimCameraOffset` di bawah dihitung LOKAL & DETERMINISTIK dari ai[1]/ai[2]
    // (yang otomatis network-synced kayak field ai[] NPC lainnya), jadi TIDAK butuh sinkronisasi
    // manual tambahan lewat SendExtraAI/ReceiveExtraAI -- semua client bakal ngitung offset yang
    // sama persis selama stage/timer-nya sama, konsisten sama pola pattern lain di file ini.
    //
    // 🔊 [ASET YANG PERLU DITAMBAHIN MANUAL]:
    //   - Sound: TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Roar (belum ada, tinggal taruh file-nya)
    // =========================================================================================
    public partial class PlutoHead
    {
        private const int SpawnStageCameraPullIn = 0;
        private const int SpawnStageRunIn = 1;
        private const int SpawnStageRoarHold = 2;
        private const int SpawnStageCameraReturn = 3;

        // 🛑 [LOKASI BALANCING JARAK TARIK KAMERA] 50 block di atas player (16px/block).
        private const float SpawnAnimCamAboveDistance = 50f * 16f;

        private const int CamPullInDuration = 40;   // ~0.67 detik
        private const int CamReturnDuration = 35;   // ~0.58 detik
        private const int RoarHoldDuration = 50;    // ~0.83 detik
        private const float SpawnRunSpeed = 70f;    // lebih cepat dari dash biasa (44f) -- kesan "nyerbu"
        private const float SpawnOffscreenDistance = 2600f; // jauh banget, pasti di luar layar berapapun zoom-nya

        // Offset kamera SAAT INI (world px) yang bakal ditambahin ke Main.screenPosition lewat
        // PlutoSpawnCameraPlayer -- HANYA dipakai/dibaca kalau IsSpawnAnimationActive true.
        public Vector2 SpawnAnimCameraOffset { get; private set; } = Vector2.Zero;

        public bool IsSpawnAnimationActive => NPC.active && NPC.ai[0] == 9f;

        // 🛑 [SINKRON BACKGROUND] Dinyalain PERSIS pas Pluto roar (Stage 2 dimulai), dipakai
        // PlutoBackgroundSystem.cs buat nentuin kapan background boss beneran "pop" -- BUKAN dari
        // awal NPC ini ke-spawn (biar reveal-nya nyambung sama momen roar, bukan duluan).
        public bool HasTriggeredBackgroundReveal { get; private set; } = false;

        private void ExecuteSpawnAnimationPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == SpawnStageCameraPullIn) {
                if (timer == 0) {
                    // --- Setup sekali di awal stage: teleport Pluto ke titik jauh di luar layar,
                    // salah satu dari 3 arah acak (atas / kiri / kanan -- TIDAK PERNAH dari bawah).
                    int side = Main.rand.Next(3); // 0 = atas, 1 = kiri, 2 = kanan
                    Vector2 spawnPos;
                    switch (side) {
                        case 0:
                            spawnPos = new Vector2(player.Center.X + Main.rand.Next(-500, 501), player.Center.Y - SpawnOffscreenDistance);
                            break;
                        case 1:
                            spawnPos = new Vector2(player.Center.X - SpawnOffscreenDistance, player.Center.Y - Main.rand.Next(0, 400));
                            break;
                        default:
                            spawnPos = new Vector2(player.Center.X + SpawnOffscreenDistance, player.Center.Y - Main.rand.Next(0, 400));
                            break;
                    }

                    NPC.Center = spawnPos;
                    NPC.velocity = Vector2.Zero;
                    NPC.alpha = 0;
                    Vector2 faceDir = (player.Center - NPC.Center).SafeNormalize(-Vector2.UnitY);
                    NPC.rotation = faceDir.ToRotation();

                    // Kunci body/tail biar ikut ke posisi baru INSTAN (ga stretch/ngaco), sama
                    // persis pola-nya kayak ExecuteTeleportDashPattern di PredicMineDash.cs.
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC segment = Main.npc[i];
                        if (segment.active && segment.ai[3] == NPC.whoAmI &&
                           (segment.type == ModContent.NPCType<PlutoBody>() || segment.type == ModContent.NPCType<PlutoTail>())) {
                            segment.Center = NPC.Center;
                            segment.netUpdate = true;
                        }
                    }

                    NPC.netUpdate = true;
                }

                float pullProgress = MathHelper.Clamp(timer / (float)CamPullInDuration, 0f, 1f);
                pullProgress = pullProgress * pullProgress * (3f - 2f * pullProgress); // smoothstep
                SpawnAnimCameraOffset = new Vector2(0f, -SpawnAnimCamAboveDistance) * pullProgress;

                timer++;
                if (timer >= CamPullInDuration) {
                    NPC.ai[1] = SpawnStageRunIn;
                    NPC.ai[2] = 0f;

                    // 🛑 [LARI MASUK] Titik kedatangan: sedikit di atas & di depan player, biar
                    // begitu nyampe langsung enak buat lanjut ke Normal Dash.
                    Vector2 arrivalPoint = player.Center + new Vector2(Main.rand.Next(-150, 151), -300f);
                    Vector2 runDir = (arrivalPoint - NPC.Center).SafeNormalize(Vector2.Zero);
                    float distanceToArrival = Vector2.Distance(NPC.Center, arrivalPoint);

                    NPC.velocity = runDir * SpawnRunSpeed;
                    NPC.rotation = runDir.ToRotation();
                    dashDuration = MathHelper.Clamp(distanceToArrival / SpawnRunSpeed, 20f, 90f);

                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageRunIn) {
                NPC.rotation = NPC.velocity.ToRotation();

                timer++;
                if (timer >= (int)dashDuration) {
                    NPC.velocity = Vector2.Zero;
                    NPC.ai[1] = SpawnStageRoarHold;
                    NPC.ai[2] = 0f;

                    // 🛑 [ROAR + REVEAL] Persis di momen ini Pluto berhenti & roar -- background
                    // boss baru "pop" dari sini (lihat HasTriggeredBackgroundReveal & pemakaiannya
                    // di PlutoBackgroundSystem.cs).
                    HasTriggeredBackgroundReveal = true;
                    SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Roar"), NPC.Center);
                    ScreenShakeSystem.StartShake(20f, 45, Vector2.Zero);

                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageRoarHold) {
                NPC.velocity = Vector2.Zero;

                timer++;
                if (timer >= RoarHoldDuration) {
                    NPC.ai[1] = SpawnStageCameraReturn;
                    NPC.ai[2] = 0f;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageCameraReturn) {
                float returnProgress = MathHelper.Clamp(timer / (float)CamReturnDuration, 0f, 1f);
                returnProgress = returnProgress * returnProgress * (3f - 2f * returnProgress); // smoothstep
                SpawnAnimCameraOffset = new Vector2(0f, -SpawnAnimCamAboveDistance) * (1f - returnProgress);

                timer++;
                if (timer >= CamReturnDuration) {
                    // --- Animasi kelar -- LANGSUNG nembak Pattern 1 (Normal Dash), TANPA lewat
                    // gacha picker (NPC.ai[0]==0f), SESUAI REQUEST ("sisanya gacha seperti biasa"
                    // artinya baru MULAI dari giliran serangan SETELAH ini).
                    SpawnAnimCameraOffset = Vector2.Zero;
                    NPC.ai[0] = 1f;
                    NPC.ai[1] = 0f;
                    NPC.ai[2] = 0f;
                    NPC.ai[3] = 0f;
                    maxDashes = Main.rand.Next(5, 9);
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
        }
    }
}
