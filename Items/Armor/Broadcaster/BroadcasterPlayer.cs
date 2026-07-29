using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Broadcaster
{
    public class BroadcasterPlayer : ModPlayer
    {
        public bool broadcasterSetEquipped = false;
        public bool isDefensiveMode = false;
        private int healCooldown = 0;

        public override void ResetEffects() {
            broadcasterSetEquipped = false;
        }

        public override void PostUpdate() {
            if (healCooldown > 0) {
                healCooldown--;
            }
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            // MENGGUNAKAN KEYBIND DARI SanityKeybinds
            if (broadcasterSetEquipped && SanityKeybinds.SwitchBroadcasterModeKey != null && SanityKeybinds.SwitchBroadcasterModeKey.JustPressed) {
                isDefensiveMode = !isDefensiveMode;
                
                SoundEngine.PlaySound(SoundID.Item149, Player.Center);

                string modeText = isDefensiveMode ? "Broadcast Mode: DEFENSE" : "Broadcast Mode: OFFENSE";
                Color textColor = isDefensiveMode ? Color.Cyan : Color.OrangeRed;
                
                CombatText.NewText(Player.getRect(), textColor, modeText, true);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (broadcasterSetEquipped && !isDefensiveMode && target.CanBeChasedBy()) {
                if (Main.rand.NextBool(2)) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Electric, 0, 0, 100, default, 1f);
                        d.noGravity = true;
                    }
                    
                    if (Player.whoAmI == Main.myPlayer) {
                        int extraDamage = 10;
                        target.SimpleStrikeNPC(extraDamage, 0, false, 0f, DamageClass.Generic, false, 0f, true);
                    }
                }
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (broadcasterSetEquipped && isDefensiveMode && healCooldown <= 0) {
                TriggerHeal();
            }
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (broadcasterSetEquipped && isDefensiveMode && healCooldown <= 0) {
                TriggerHeal();
            }
            return base.FreeDodge(info);
        }

        private void TriggerHeal() {
            Player.Heal(3);
            healCooldown = 60; // 1 Detik Cooldown
            
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.WhiteTorch, 0, -1f, 0, default, 0.9f);
                d.noGravity = true;
            }
        }
    }
}