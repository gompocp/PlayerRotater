﻿using System.Collections.Generic;
using ActionMenuApi;

namespace PlayerRotater
{

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using MelonLoader;

    using PlayerRotater.ControlSchemes;

    using UIExpansionKit.API;

    using UnityEngine;

    public class ModMain : MelonMod
    {

        private const string SettingsIdentifier = "PlayerRotater";

        /// <summary>
        ///     https://www.youtube.com/watch?v=U06jlgpMtQs
        /// </summary>
        private static MelonPreferences_Category ourCategory;

        private static MelonPreferences_Entry<bool> noClippingEntry, invertPitchEntry;

        private static MelonPreferences_Entry<float> flyingSpeedEntry, rotationSpeedEntry;

        private static MelonPreferences_Entry<string> controlSchemeEntry, rotationOriginEntry;

        private List<(string SettingsValue, string DisplayName)> controlSchemes, rotationOrigins;

        private bool failedToLoad;

        private static bool easterEgg;

        public override void OnApplicationStart()
        {
            Utilities.IsInVR = Environment.GetCommandLineArgs().All(args => !args.Equals("--no-vr", StringComparison.OrdinalIgnoreCase));
            easterEgg = Environment.GetCommandLineArgs().Any(arg => arg.IndexOf("barrelroll", StringComparison.OrdinalIgnoreCase) != -1);
            
            if (!RotationSystem.Initialize())
            {
                MelonLogger.Msg("Failed to initialize the rotation system. Instance already exists");
                failedToLoad = true;
                return;
            }

            controlSchemes = new List<(string SettingsValue, string DisplayName)> { ("default", "Default"), ("jannyaa", "JanNyaa's") };
            rotationOrigins = new List<(string SettingsValue, string DisplayName)>
                                  {
                                      ("hips", "Hips"), ("viewpoint", "View Point/Camera"), ("righthand", "Right Hand"), ("lefthand", "Left Hand")
                                  };

            ModPatches.Patch(Harmony);
            SetupUI();

            SetupSettings();
            AMAPI.AddSubMenuToMenu(ActionMenuPageType.Main, () =>
            {
                
                RotationSystem.Instance.playerTransform = Utilities.GetLocalVRCPlayer().transform;
                Utilities.AlignTrackingToPlayerDelegate alignTrackingToPlayer = Utilities.GetAlignTrackingToPlayerDelegate;
                AMAPI.AddRadialPedalToSubMenu((f) =>
                {
                    var eulerAngles = RotationSystem.Instance.playerTransform.eulerAngles;
                    RotationSystem.Instance.playerTransform.eulerAngles = new Vector3((f / 100f)*360f, eulerAngles.y, eulerAngles.z);
                    alignTrackingToPlayer.Invoke();
                }, "Rotate X", (RotationSystem.Instance.playerTransform.eulerAngles.x/360f)*100f);
                AMAPI.AddRadialPedalToSubMenu((f) =>
                {
                    var eulerAngles = RotationSystem.Instance.playerTransform.eulerAngles;
                    RotationSystem.Instance.playerTransform.eulerAngles = new Vector3(eulerAngles.x, (f / 100f)*360f, eulerAngles.z);
                    alignTrackingToPlayer.Invoke();
                }, "Rotate Y", (RotationSystem.Instance.playerTransform.eulerAngles.y/360f)*100f);
                AMAPI.AddRadialPedalToSubMenu((f) =>
                {
                    var eulerAngles = RotationSystem.Instance.playerTransform.eulerAngles;
                    RotationSystem.Instance.playerTransform.eulerAngles = new Vector3(eulerAngles.x, eulerAngles.y, (f / 100f)*360f);
                    alignTrackingToPlayer.Invoke();
                }, "Rotate Z", (RotationSystem.Instance.playerTransform.eulerAngles.z/360f)*100f);
                AMAPI.AddFourAxisPedalToSubMenu("Translate XY", Vector2.zero, (v) =>
                {
                    RotationSystem.Instance.playerTransform.localPosition += (Vector3) (v / 25);
                }, null, "Y+", "X+", "Y-", "X-");
                AMAPI.AddFourAxisPedalToSubMenu("Translate XY", Vector2.zero, (v) =>
                {
                    RotationSystem.Instance.playerTransform.localPosition += (Vector3) (v / 25);
                }, null, "Y+", "X+", "Y-", "X-");
                AMAPI.AddFourAxisPedalToSubMenu("Translate ZY", Vector2.zero, (v) =>
                {
                    RotationSystem.Instance.playerTransform.localPosition += new Vector3(0, v.y/25, v.x/25);
                }, null, "Y+", "Z+", "Y-", "Z-");
                AMAPI.AddFourAxisPedalToSubMenu("Translate XZ", Vector2.zero, (v) =>
                {
                    RotationSystem.Instance.playerTransform.localPosition += new Vector3(v.x/25, 0, v.y/25);
                }, null, "Z+", "X+", "Z-", "X-");
                AMAPI.AddTogglePedalToSubMenu((b) =>
                {
                    RotationSystem.Instance.Toggle();
                }, false, "Toggle");
            }, "Player Rotator");
        }
        private static float xRotation = 0;
        private static float yRotation = 0;
        private static float zRotation = 0;

        public override void OnPreferencesSaved()
        {
            if (failedToLoad) return;
            LoadSettings();
        }

        private void SetupSettings()
        {
            if (failedToLoad) return;

            ourCategory = MelonPreferences.CreateCategory(SettingsIdentifier, BuildInfo.Name);
            noClippingEntry = ourCategory.CreateEntry("NoClip", RotationSystem.NoClipFlying, "No-Clipping (Desktop)") as MelonPreferences_Entry<bool>;
            rotationSpeedEntry = ourCategory.CreateEntry("RotationSpeed", RotationSystem.RotationSpeed, "Rotation Speed") as MelonPreferences_Entry<float>;
            flyingSpeedEntry = ourCategory.CreateEntry("FlyingSpeed", RotationSystem.FlyingSpeed, "Flying Speed") as MelonPreferences_Entry<float>;
            invertPitchEntry = ourCategory.CreateEntry("InvertPitch", RotationSystem.InvertPitch, "Invert Pitch") as MelonPreferences_Entry<bool>;

            controlSchemeEntry = ourCategory.CreateEntry("ControlScheme", "default", "Control Scheme") as MelonPreferences_Entry<string>;
            ExpansionKitApi.RegisterSettingAsStringEnum(ourCategory.Identifier, controlSchemeEntry?.Identifier, controlSchemes);

            rotationOriginEntry = ourCategory.CreateEntry("RotationOrigin", "hips", "Humanoid Rotation Origin") as MelonPreferences_Entry<string>;
            ExpansionKitApi.RegisterSettingAsStringEnum(ourCategory.Identifier, rotationOriginEntry?.Identifier, rotationOrigins);

            LoadSettings();
        }

        private static void LoadSettings()
        {
            try
            {
                RotationSystem.NoClipFlying = noClippingEntry.Value;
                RotationSystem.RotationSpeed = rotationSpeedEntry.Value;
                RotationSystem.FlyingSpeed = flyingSpeedEntry.Value;
                RotationSystem.InvertPitch = invertPitchEntry.Value;

                switch (controlSchemeEntry.Value)
                {
                    default:
                        controlSchemeEntry.ResetToDefault();
                        controlSchemeEntry.Save();

                        RotationSystem.CurrentControlScheme = new DefaultControlScheme();
                        break;

                    case "default":
                        RotationSystem.CurrentControlScheme = new DefaultControlScheme();
                        break;

                    case "jannyaa":
                        RotationSystem.CurrentControlScheme = new JanNyaaControlScheme();
                        break;
                }

                switch (rotationOriginEntry.Value)
                {
                    default:
                        rotationOriginEntry.ResetToDefault();
                        rotationOriginEntry.Save();

                        RotationSystem.RotationOrigin = RotationSystem.RotationOriginEnum.Hips;
                        break;

                    case "hips":
                        RotationSystem.RotationOrigin = RotationSystem.RotationOriginEnum.Hips;
                        break;

                    case "viewpoint":
                        RotationSystem.RotationOrigin = RotationSystem.RotationOriginEnum.ViewPoint;
                        break;

                    // ReSharper disable once StringLiteralTypo
                    case "righthand":
                        RotationSystem.RotationOrigin = RotationSystem.RotationOriginEnum.RightHand;
                        break;

                    // ReSharper disable once StringLiteralTypo
                    case "lefthand":
                        RotationSystem.RotationOrigin = RotationSystem.RotationOriginEnum.LeftHand;
                        break;
                }

                RotationSystem.Instance.UpdateSettings();
            }
            catch (Exception e)
            {
                MelonLogger.Msg("Failed to Load Settings: " + e);
            }
        }

        private static void SetupUI()
        {
            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.QuickMenu).AddSimpleButton("Toggle\nPlayer\nRotation", () => RotationSystem.Instance.Toggle());

            // shhhhhhh (✿❦ ͜ʖ ❦)
            if (easterEgg)
                ExpansionKitApi.GetExpandedMenu(ExpandedMenu.QuickMenu).AddSimpleButton("Do A\nBarrel Roll", () => RotationSystem.Instance.BarrelRoll());
        }
        

        public override void OnUpdate()
        {
            if (failedToLoad) return;
            RotationSystem.Instance.Update();
            if (!easterEgg) return;
            if (RotationSystem.BarrelRolling) return;
            if (!Input.GetKeyDown(KeyCode.B)) return;
            
            if (Input.GetKey(KeyCode.LeftShift)
                && Input.GetKey(KeyCode.LeftControl))
            {
                RotationSystem.Instance.BarrelRoll();
            }
            //RotationSystem.Instance.OnUpdate();
        }

    }

}