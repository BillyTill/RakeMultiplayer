using System;
using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Display, window, refresh-rate and quality settings for the in-game menu.
    /// Values are applied immediately and stored in PlayerPrefs, so they survive
    /// restarts. No display change is made until the player presses Apply.
    /// </summary>
    public static class DisplaySettings
    {
        private const string Key = "RakeMP.Display.";

        public static int Width;
        public static int Height;
        public static int RefreshRate;
        public static bool Fullscreen;
        public static int Quality;
        public static int VSync;
        public static int AntiAliasing;

        public static bool HasSaved { get; private set; }

        private static readonly List<Resolution> Resolutions = new List<Resolution>();
        private static readonly List<int> RefreshRates = new List<int>();

        public static void Load()
        {
            Resolutions.Clear();
            RefreshRates.Clear();

            // Screen.currentResolution is the desktop size. In windowed mode the
            // actual player window can be smaller, and that is what the menu shows.
            int windowW = Screen.width > 0 ? Screen.width : Screen.currentResolution.width;
            int windowH = Screen.height > 0 ? Screen.height : Screen.currentResolution.height;
            int defaultW = Screen.fullScreen ? Screen.currentResolution.width : windowW;
            int defaultH = Screen.fullScreen ? Screen.currentResolution.height : windowH;

            HasSaved = PlayerPrefs.GetInt(Key + "Saved", 0) == 1;
            if (HasSaved)
            {
                Width = PlayerPrefs.GetInt(Key + "Width", defaultW);
                Height = PlayerPrefs.GetInt(Key + "Height", defaultH);
                RefreshRate = PlayerPrefs.GetInt(Key + "Refresh", Screen.currentResolution.refreshRate);
                Fullscreen = PlayerPrefs.GetInt(Key + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
                Quality = PlayerPrefs.GetInt(Key + "Quality", QualitySettings.GetQualityLevel());
                VSync = PlayerPrefs.GetInt(Key + "VSync", QualitySettings.vSyncCount);
                AntiAliasing = PlayerPrefs.GetInt(Key + "AA", QualitySettings.antiAliasing);
            }
            else
            {
                Width = defaultW;
                Height = defaultH;
                RefreshRate = Screen.currentResolution.refreshRate;
                Fullscreen = Screen.fullScreen;
                Quality = QualitySettings.GetQualityLevel();
                VSync = QualitySettings.vSyncCount;
                AntiAliasing = QualitySettings.antiAliasing;
            }

            BuildResolutionList();
            BuildRefreshList();
            ClampAll();
        }

        public static void ApplySaved()
        {
            if (!HasSaved) return;
            Apply();
        }

        public static void SaveAndApply()
        {
            Apply();
            PlayerPrefs.SetInt(Key + "Width", Width);
            PlayerPrefs.SetInt(Key + "Height", Height);
            PlayerPrefs.SetInt(Key + "Refresh", RefreshRate);
            PlayerPrefs.SetInt(Key + "Fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Key + "Quality", Quality);
            PlayerPrefs.SetInt(Key + "VSync", VSync);
            PlayerPrefs.SetInt(Key + "AA", AntiAliasing);
            PlayerPrefs.SetInt(Key + "Saved", 1);
            PlayerPrefs.Save();
            HasSaved = true;
        }

        private static void Apply()
        {
            try
            {
                Screen.SetResolution(Width, Height, Fullscreen, RefreshRate);
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Display] SetResolution failed: " + ex.Message);
            }

            try
            {
                QualitySettings.SetQualityLevel(Quality, true);
                QualitySettings.vSyncCount = VSync;
                QualitySettings.antiAliasing = AntiAliasing;
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Display] quality apply failed: " + ex.Message);
            }
        }

        private static void BuildResolutionList()
        {
            Resolutions.Clear();
            var source = Screen.resolutions;
            for (int i = 0; i < source.Length; i++)
            {
                var r = source[i];
                bool exists = false;
                for (int j = 0; j < Resolutions.Count; j++)
                {
                    if (Resolutions[j].width == r.width && Resolutions[j].height == r.height)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) Resolutions.Add(r);
            }

            if (Resolutions.Count == 0)
            {
                var current = Screen.currentResolution;
                Resolutions.Add(current);
            }

            // Keep the current window size selectable even if the monitor list omits it.
            bool hasCurrent = false;
            for (int i = 0; i < Resolutions.Count; i++)
            {
                if (Resolutions[i].width == Width && Resolutions[i].height == Height) hasCurrent = true;
            }
            if (!hasCurrent)
            {
                var current = Screen.currentResolution;
                current.width = Width;
                current.height = Height;
                current.refreshRate = RefreshRate;
                Resolutions.Add(current);
            }

            Resolutions.Sort(delegate(Resolution a, Resolution b)
            {
                int byWidth = b.width.CompareTo(a.width);
                return byWidth != 0 ? byWidth : b.height.CompareTo(a.height);
            });
        }

        private static void BuildRefreshList()
        {
            RefreshRates.Clear();
            var source = Screen.resolutions;
            for (int i = 0; i < source.Length; i++)
            {
                var r = source[i];
                if (r.width != Width || r.height != Height) continue;
                if (r.refreshRate <= 0) continue;
                if (!RefreshRates.Contains(r.refreshRate)) RefreshRates.Add(r.refreshRate);
            }

            if (RefreshRates.Count == 0)
            {
                int current = Screen.currentResolution.refreshRate;
                RefreshRates.Add(current > 0 ? current : 60);
            }
            if (RefreshRate <= 0) RefreshRate = RefreshRates[0];
            if (!RefreshRates.Contains(RefreshRate)) RefreshRates.Add(RefreshRate);

            RefreshRates.Sort();
        }

        private static void ClampAll()
        {
            if (Quality < 0) Quality = 0;
            if (Quality >= QualitySettings.names.Length) Quality = Mathf.Max(0, QualitySettings.names.Length - 1);
            if (VSync < 0 || VSync > 2) VSync = 0;
            if (AntiAliasing != 0 && AntiAliasing != 2 && AntiAliasing != 4 && AntiAliasing != 8) AntiAliasing = 0;
        }

        public static void CycleResolution(int direction)
        {
            if (Resolutions.Count == 0) return;
            int index = 0;
            for (int i = 0; i < Resolutions.Count; i++)
            {
                if (Resolutions[i].width == Width && Resolutions[i].height == Height) { index = i; break; }
            }
            index = (index + direction + Resolutions.Count) % Resolutions.Count;
            Width = Resolutions[index].width;
            Height = Resolutions[index].height;

            BuildRefreshList();
            if (!RefreshRates.Contains(RefreshRate)) RefreshRate = RefreshRates[0];
        }

        public static void CycleFullscreen()
        {
            Fullscreen = !Fullscreen;
        }

        public static void CycleRefresh(int direction)
        {
            if (RefreshRates.Count == 0) return;
            int index = RefreshRates.IndexOf(RefreshRate);
            if (index < 0) index = 0;
            index = (index + direction + RefreshRates.Count) % RefreshRates.Count;
            RefreshRate = RefreshRates[index];
        }

        public static void CycleQuality(int direction)
        {
            int count = Mathf.Max(1, QualitySettings.names.Length);
            Quality = (Quality + direction + count) % count;
        }

        public static void CycleVSync(int direction)
        {
            int[] values = { 0, 1, 2 };
            int index = Array.IndexOf(values, VSync);
            if (index < 0) index = 0;
            index = (index + direction + values.Length) % values.Length;
            VSync = values[index];
        }

        public static void CycleAntiAliasing(int direction)
        {
            int[] values = { 0, 2, 4, 8 };
            int index = Array.IndexOf(values, AntiAliasing);
            if (index < 0) index = 0;
            index = (index + direction + values.Length) % values.Length;
            AntiAliasing = values[index];
        }

        public static string ResolutionLabel
        {
            get { return Width + " × " + Height; }
        }

        public static string ModeLabel
        {
            get { return Fullscreen ? "Fullscreen" : "Windowed"; }
        }

        public static string RefreshLabel
        {
            get { return RefreshRate > 0 ? RefreshRate + " Hz" : "Default"; }
        }

        public static string QualityLabel
        {
            get
            {
                if (QualitySettings.names == null || QualitySettings.names.Length == 0) return "Quality " + Quality;
                return QualitySettings.names[Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1)];
            }
        }

        public static string VSyncLabel
        {
            get
            {
                if (VSync == 0) return "Off";
                if (VSync == 2) return "Every 2nd VBlank";
                return "On";
            }
        }

        public static string AntiAliasingLabel
        {
            get { return AntiAliasing <= 0 ? "Off" : AntiAliasing + "×"; }
        }
    }
}
