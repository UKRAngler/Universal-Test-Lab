using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UniversalTestLab
{
    internal static class Theme
    {
        public static readonly Color Window = Color.FromArgb(24, 30, 38);
        public static readonly Color Surface = Color.FromArgb(34, 43, 54);
        public static readonly Color Surface2 = Color.FromArgb(43, 54, 67);
        public static readonly Color Surface3 = Color.FromArgb(53, 66, 80);
        public static readonly Color Border = Color.FromArgb(68, 82, 98);
        public static readonly Color Text = Color.FromArgb(226, 232, 238);
        public static readonly Color Muted = Color.FromArgb(157, 170, 182);
        public static readonly Color Accent = Color.FromArgb(211, 171, 55);
        public static readonly Color AccentDark = Color.FromArgb(145, 112, 28);
        public static readonly Color Good = Color.FromArgb(77, 157, 111);
        public static readonly Color Danger = Color.FromArgb(197, 83, 74);

        public static void Button(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? AccentDark : Surface3;
            button.ForeColor = Text;
            button.Cursor = Cursors.Hand;
        }

        public static void Input(Control control)
        {
            control.BackColor = Surface;
            control.ForeColor = Text;
        }

        public static Label Label(string text, bool title)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = title ? Text : Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = title ? new Font("Segoe UI Semibold", 10.5f) : new Font("Segoe UI", 9f)
            };
        }
    }

    internal sealed class Aircraft
    {
        public string Id;
        public string Display;
        public string Type;
        public string DefaultPreset;
        public string Nation;
        public int Rank;
        public double MaxLoad;
        public override string ToString() { return Display; }
    }

    internal sealed class TargetUnit
    {
        public string Id;
        public string Display;
        public string DefaultPreset;
        public override string ToString() { return Display; }
    }

    internal sealed class DonorWeapon
    {
        public string AircraftId;
        public string AircraftDisplay;
        public int Slot;
        public string Mount;
        public string Trigger;
        public string Blk;
        public string Emitter;
        public int Bullets;
        public string Icon;
        public string Name;
        public string Category;
        public string Nations;
        public double UnitMass;
        public double TotalMass;
        public override string ToString() { return Name; }
    }

    internal sealed class PylonSlot
    {
        public string AircraftId;
        public int Slot;
        public int Order;
        public int Tier;
        public double MaxLoad;
        public string AnchorMount;
    }

    internal sealed class PylonAssignment
    {
        public PylonSlot Pylon;
        public DonorWeapon Weapon;
        public bool Injected;
    }

    internal sealed class GeneratedAircraft
    {
        public string ClassId;
        public string PresetId;
        public string FlightModelPath;
        public string PresetPath;
        public int SpawnSpeedKmh;
    }

    internal sealed class SavedPresetEntry
    {
        public int Slot;
        public bool Injected;
        public string Mount;
        public string Trigger;
        public string Blk;
        public string Emitter;
        public int Bullets;
        public string Icon;
        public string Name;
        public string Category;
        public double UnitMass;
        public double TotalMass;
    }

    internal sealed class SavedPreset
    {
        public string Name;
        public string AircraftId;
        public readonly List<SavedPresetEntry> Entries = new List<SavedPresetEntry>();
        public override string ToString() { return Name; }
    }

    internal static class PresetStore
    {
        public static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTestLab", "custom_presets.tsv"); }
        }

        private static string B64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        private static string FromB64(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        public static List<SavedPreset> Load()
        {
            List<SavedPreset> result = new List<SavedPreset>();
            if (!File.Exists(FilePath)) return result;
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                try
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 3) continue;
                    SavedPreset preset = new SavedPreset { Name = FromB64(p[0]), AircraftId = FromB64(p[1]) };
                    string payload = FromB64(p[2]);
                    foreach (string record in payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] e = record.Split('|');
                        int slot, bullets;
                        if (e.Length < 12 || !Int32.TryParse(e[0], out slot) || !Int32.TryParse(e[6], out bullets)) continue;
                        preset.Entries.Add(new SavedPresetEntry
                        {
                            Slot = slot, Injected = e[1] == "1", Mount = FromB64(e[2]), Trigger = FromB64(e[3]), Blk = FromB64(e[4]),
                            Emitter = FromB64(e[5]), Bullets = bullets, Icon = FromB64(e[7]), Name = FromB64(e[8]), Category = FromB64(e[9]),
                            UnitMass = MainForm.ParseNumber(e[10]), TotalMass = MainForm.ParseNumber(e[11])
                        });
                    }
                    if (!String.IsNullOrWhiteSpace(preset.Name) && !String.IsNullOrWhiteSpace(preset.AircraftId)) result.Add(preset);
                }
                catch { }
            }
            return result.OrderBy(x => x.Name).ToList();
        }

        public static void Save(IEnumerable<SavedPreset> presets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            List<string> lines = new List<string>();
            foreach (SavedPreset preset in presets.OrderBy(x => x.Name))
            {
                StringBuilder payload = new StringBuilder();
                foreach (SavedPresetEntry e in preset.Entries.OrderBy(x => x.Slot))
                {
                    payload.Append(e.Slot.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(e.Injected ? "1" : "0").Append('|').Append(B64(e.Mount)).Append('|').Append(B64(e.Trigger)).Append('|')
                        .Append(B64(e.Blk)).Append('|').Append(B64(e.Emitter)).Append('|').Append(e.Bullets.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(B64(e.Icon)).Append('|').Append(B64(e.Name)).Append('|').Append(B64(e.Category)).Append('|')
                        .Append(e.UnitMass.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(e.TotalMass.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                }
                lines.Add(B64(preset.Name) + "\t" + B64(preset.AircraftId) + "\t" + B64(payload.ToString()));
            }
            File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
        }
    }

    internal static class SettingsStore
    {
        public static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTestLab", "game_folder.txt"); }
        }

        public static string LoadGameFolder()
        {
            try
            {
                if (!File.Exists(FilePath)) return "";
                string path = File.ReadAllText(FilePath, Encoding.UTF8).Trim().Trim('"');
                if (String.IsNullOrWhiteSpace(path)) return "";
                path = Path.GetFullPath(path);
                return File.Exists(Path.Combine(path, "aces.vromfs.bin")) ? path : "";
            }
            catch { return ""; }
        }

        public static void SaveGameFolder(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return;
                path = Path.GetFullPath(path.Trim().Trim('"'));
                if (!File.Exists(Path.Combine(path, "aces.vromfs.bin"))) return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, path, new UTF8Encoding(false));
            }
            catch { }
        }
    }

    internal sealed class BlockSpan
    {
        public int Start;
        public int Open;
        public int End;
        public string Text;
    }

    internal static class Embedded
    {
        public static byte[] Bytes(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Embedded resource is missing: " + name);
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        public static string Text(string name) { return Encoding.UTF8.GetString(Bytes(name)); }
    }

    internal static class BlkTools
    {
        public static int MatchingBrace(string text, int open)
        {
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        public static BlockSpan FirstBlock(string text, string name, int from)
        {
            Match match = Regex.Match(text.Substring(from), @"(?m)^\s*" + Regex.Escape(name) + @"\s*\{");
            if (!match.Success) return null;
            int start = from + match.Index;
            int open = text.IndexOf('{', start);
            int end = MatchingBrace(text, open);
            if (open < 0 || end < 0) return null;
            return new BlockSpan { Start = start, Open = open, End = end, Text = text.Substring(start, end - start + 1) };
        }

        public static List<BlockSpan> Blocks(string text, string name)
        {
            List<BlockSpan> result = new List<BlockSpan>();
            foreach (Match match in Regex.Matches(text, @"(?m)^\s*" + Regex.Escape(name) + @"\s*\{"))
            {
                int open = text.IndexOf('{', match.Index);
                int end = MatchingBrace(text, open);
                if (open >= 0 && end >= 0)
                    result.Add(new BlockSpan { Start = match.Index, Open = open, End = end, Text = text.Substring(match.Index, end - match.Index + 1) });
            }
            return result;
        }

        public static string Field(string text, string field, string type)
        {
            Match match = Regex.Match(text, Regex.Escape(field) + ":" + Regex.Escape(type) + @"\s*=\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        public static string ReplaceStringField(string block, string field, string value)
        {
            Regex regex = new Regex("(" + Regex.Escape(field) + @":t\s*=\s*"")[^""]*("")");
            if (!regex.IsMatch(block)) throw new InvalidOperationException("BLK field not found: " + field);
            return regex.Replace(block, delegate(Match m) { return m.Groups[1].Value + value + m.Groups[2].Value; }, 1);
        }

        public static string ReplaceIntField(string block, string field, int value)
        {
            Regex regex = new Regex("(" + Regex.Escape(field) + @":i\s*=\s*)-?\d+");
            if (!regex.IsMatch(block)) throw new InvalidOperationException("BLK field not found: " + field);
            return regex.Replace(block, delegate(Match m) { return m.Groups[1].Value + value.ToString(CultureInfo.InvariantCulture); }, 1);
        }

        public static string ReplaceSpan(string text, BlockSpan span, string replacement)
        {
            return text.Substring(0, span.Start) + replacement + text.Substring(span.End + 1);
        }

        public static BlockSpan UnitBlockByName(string text, string unitName)
        {
            string needle = "name:t=\"" + unitName + "\"";
            int nameAt = text.IndexOf(needle, StringComparison.Ordinal);
            if (nameAt < 0) throw new InvalidOperationException("Mission unit not found: " + unitName);
            string[] kinds = { "armada", "tankModels", "ships", "wheeled_vehicles", "structures" };
            int best = -1;
            foreach (string kind in kinds)
            {
                int at = text.LastIndexOf("  " + kind + "{", nameAt, StringComparison.Ordinal);
                if (at > best) best = at;
            }
            if (best < 0) throw new InvalidOperationException("Mission unit block not found: " + unitName);
            int open = text.IndexOf('{', best);
            int end = MatchingBrace(text, open);
            if (end < nameAt) throw new InvalidOperationException("Mission unit block is damaged: " + unitName);
            return new BlockSpan { Start = best, Open = open, End = end, Text = text.Substring(best, end - best + 1) };
        }

        public static string UpdateUnit(string text, string name, string unitClass, string preset, int count)
        {
            BlockSpan span = UnitBlockByName(text, name);
            string block = ReplaceStringField(span.Text, "unit_class", unitClass);
            block = ReplaceStringField(block, "weapons", preset);
            block = ReplaceIntField(block, "count", count);
            return ReplaceSpan(text, span, block);
        }

        public static string MakeGroundTargetHostile(string text, string name)
        {
            BlockSpan unit = UnitBlockByName(text, name);
            string block = new Regex(@"crewSkillK:r\s*=\s*[0-9.]+").Replace(unit.Text, "crewSkillK:r=1", 1);
            block = new Regex(@"applyAllMods:b\s*=\s*(?:no|false)").Replace(block, "applyAllMods:b=yes", 1);
            block = new Regex(@"attack_type:t\s*=\s*""[^""]*""").Replace(block, "attack_type:t=\"fire_at_will\"", 1);
            text = ReplaceSpan(text, unit, block);
            BlockSpan triggers = FirstBlock(text, "triggers", 0);
            if (triggers == null) throw new InvalidOperationException("Mission triggers block is missing.");
            string trigger = @"
  ""UTL Hostile Ground Target""{
    is_enabled:b=yes
    comments:t=""Keep the selected enemy ground unit actively engaging the player""
    props{
      actionsType:t=""PERFORM_ONE_BY_ONE""
      conditionsType:t=""ALL""
      enableAfterComplete:b=yes
    }
    events{ periodicEvent{ time:r=1 } }
    conditions{}
    actions{
      unitSetProperties{
        object:t=""" + name + @"""
        isImmortal:b=no
        attack_type:t=""fire_at_will""
      }
      unitAttackTarget{
        playerAttracted:b=yes
        object:t=""" + name + @"""
        target:t=""You""
        fireRandom:b=no
        fireMode:t=""auto""
      }
    }
    else_actions{}
  }
";
            return text.Insert(triggers.End, trigger);
        }

        public static string DisablePlayerSwitch(string text)
        {
            int marker = text.IndexOf("comments:t=\"UTL_PLAYER_SWITCH\"", StringComparison.Ordinal);
            if (marker < 0) throw new InvalidOperationException("UTL player-switch marker is missing.");
            int triggerStart = text.LastIndexOf("\"Universal aircraft switch\"{", marker, StringComparison.Ordinal);
            int enabled = text.IndexOf("is_enabled:b=", triggerStart, StringComparison.Ordinal);
            if (triggerStart < 0 || enabled < 0 || enabled > marker) throw new InvalidOperationException("Player-switch trigger is invalid.");
            int valueStart = enabled + "is_enabled:b=".Length;
            int valueEnd = text.IndexOfAny(new[] { '\r', '\n' }, valueStart);
            if (valueEnd < 0) valueEnd = text.Length;
            return text.Substring(0, valueStart) + "no" + text.Substring(valueEnd);
        }

        public static string UpdateMissionLabels(string text, string name, string description)
        {
            Regex nameRegex = new Regex(@"(locName:t\s*=\s*"")[^""]*("")");
            Regex descRegex = new Regex(@"(locDesc:t\s*=\s*"")[^""]*("")");
            text = nameRegex.Replace(text, delegate(Match m) { return m.Groups[1].Value + name.Replace("\"", "'") + m.Groups[2].Value; }, 1);
            return descRegex.Replace(text, delegate(Match m) { return m.Groups[1].Value + description.Replace("\"", "'") + m.Groups[2].Value; }, 1);
        }

        public static string CleanLegacyMenuKeys(string text)
        {
            text = Regex.Replace(text, @"campaign:t\s*=\s*""(?:UniversalTestLab|CleanTestDrive)""", "campaign:t=\"UserMissions\"");
            text = Regex.Replace(text, @"(?m)^[ \t]*chapter:t\s*=\s*""TestDrive""[ \t]*\r?\n", "");
            return text;
        }

        public static string AddFpvDetonationTriggers(string text)
        {
            BlockSpan triggers = FirstBlock(text, "triggers", 0);
            if (triggers == null) throw new InvalidOperationException("Mission triggers block is missing.");
            StringBuilder result = new StringBuilder();
            string[] groundTargets =
            {
                "Target_01", "Target_02", "Target_03", "Target_04", "Target_05", "Target_06", "Target_07",
                "AI_Shooting_01", "AI_Shooting_02", "AI_Shooting_03", "AI_Shooting_04",
                "AI_Target_01", "AI_Target_02", "AI_Target_03", "AI_Target_04", "AI_Driving"
            };
            foreach (string target in groundTargets) result.Append(FpvTargetTrigger(target, target, 6));
            foreach (string target in new[] { "Target_Air_01", "Target_Air_02", "Heli_Target", "Heli_Target_02" })
                result.Append(FpvTargetTrigger(target, target, 8));
            result.Append(FpvTargetTrigger("Ship_Target", "Ship_Target", 45));
            result.Append(FpvDeathEffectTrigger());
            result.Append(FpvRespawnRearmTrigger());
            return text.Insert(triggers.End, result.ToString());
        }

        private static string FpvTargetTrigger(string label, string target, int distance)
        {
            return @"
  ""UTL FPV Detonation - " + label + @"""{
    is_enabled:b=yes
    comments:t=""Detonate the FPV only when it reaches this target""
    props{
      actionsType:t=""PERFORM_ONE_BY_ONE""
      conditionsType:t=""ALL""
      enableAfterComplete:b=yes
    }
    events{ periodicEvent{ time:r=0.01 } }
    conditions{
      unitDistanceBetween{
        value:r=" + distance.ToString(CultureInfo.InvariantCulture) + @"
        math:t=""3D""
        object_type:t=""any""
        target_type:t=""any""
        check_objects:t=""any""
        check_targets:t=""any""
        object_marking:i=0
        target_marking:i=0
        object_var_name:t=""""
        object_var_comp_op:t=""equal""
        object_var_value:i=0
        object:t=""You""
        target:t=""" + target + @"""
      }
    }
    actions{
      unitDamage{
        power:r=0.35
        useEffect:b=false
        countEffects:i=1
        delay:p2=1, 1
        offset:p3=0, 0, 0
        radiusOffset:p2=0, 0
        target:t=""" + target + @"""
        randomTargetsCount:i=1
        doExplosion:b=true
      }
      unitDamage{
        power:r=1
        useEffect:b=false
        countEffects:i=1
        delay:p2=1, 1
        offset:p3=0, 0, 0
        radiusOffset:p2=0, 0
        target:t=""You""
        doExplosion:b=true
      }
    }
    else_actions{}
  }
";
        }

        private static string FpvDeathEffectTrigger()
        {
            return @"
  ""UTL FPV Detonation Effect""{
    is_enabled:b=yes
    comments:t=""Show one local HEAT explosion whenever the FPV is destroyed""
    props{
      actionsType:t=""PERFORM_ONE_BY_ONE""
      conditionsType:t=""ALL""
      enableAfterComplete:b=no
    }
    events{ periodicEvent{ time:r=0.02 } }
    conditions{
      unitWhenStatus{
        object_type:t=""isKilled""
        check_objects:t=""any""
        object_marking:i=0
        object_var_name:t=""""
        object_var_comp_op:t=""equal""
        object_var_value:i=0
        target_type:t=""isAlive""
        check_period:r=0.02
        object:t=""You""
      }
    }
    actions{
      unitPlayEffect{
        effect_type:t=""specify""
        effect:t=""hit_81_132mm_heat""
        offset:p3=0, 0, 0
        radiusOffset:p2=0, 0
        show:b=true
        attach:b=false
        scale:r=1
        loopSpawn:b=false
        delay:p2=1, 1
        target:t=""You""
      }
    }
    else_actions{}
  }
";
        }

        private static string FpvRespawnRearmTrigger()
        {
            return @"
  ""UTL FPV Re-arm Detonator""{
    is_enabled:b=yes
    comments:t=""Re-enable the one-shot FPV explosion after each respawn""
    props{
      actionsType:t=""PERFORM_ONE_BY_ONE""
      conditionsType:t=""ALL""
      enableAfterComplete:b=yes
    }
    events{ periodicEvent{ time:r=0.1 } }
    conditions{
      unitWhenRespawn{
        object_var_name:t=""""
        object_var_comp_op:t=""equal""
        object:t=""You""
      }
    }
    actions{
      triggerEnable{ target:t=""UTL FPV Detonation Effect"" }
    }
    else_actions{}
  }
";
        }

        public static string RemoveBotNotifications(string text)
        {
            List<BlockSpan> remove = new List<BlockSpan>();
            foreach (BlockSpan hint in Blocks(text, "playHint"))
            {
                string name = Field(hint.Text, "name", "t") ?? "";
                if (name.IndexOf("Respawning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Rearmed", StringComparison.OrdinalIgnoreCase) >= 0)
                    remove.Add(hint);
            }
            foreach (BlockSpan hint in remove.OrderByDescending(x => x.Start))
                text = text.Remove(hint.Start, hint.End - hint.Start + 1);
            return text;
        }
    }

    internal sealed class AircraftPreview : Panel
    {
        public Aircraft Aircraft;

        public AircraftPreview()
        {
            DoubleBuffered = true;
            BackColor = Theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle rect = ClientRectangle;
            using (LinearGradientBrush bg = new LinearGradientBrush(rect, Theme.Surface3, Theme.Surface, 90f)) e.Graphics.FillRectangle(bg, rect);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = rect.Width / 2;
            int cy = Math.Max(55, rect.Height / 2 - 4);
            Point[] silhouette =
            {
                new Point(cx, cy - 47), new Point(cx + 9, cy - 12), new Point(cx + 73, cy + 16),
                new Point(cx + 77, cy + 25), new Point(cx + 18, cy + 15), new Point(cx + 13, cy + 42),
                new Point(cx + 30, cy + 51), new Point(cx + 29, cy + 57), new Point(cx, cy + 50),
                new Point(cx - 29, cy + 57), new Point(cx - 30, cy + 51), new Point(cx - 13, cy + 42),
                new Point(cx - 18, cy + 15), new Point(cx - 77, cy + 25), new Point(cx - 73, cy + 16),
                new Point(cx - 9, cy - 12)
            };
            using (SolidBrush plane = new SolidBrush(Color.FromArgb(100, 151, 168, 184))) e.Graphics.FillPolygon(plane, silhouette);
            using (Pen edge = new Pen(Color.FromArgb(160, 195, 207, 216), 1.5f)) e.Graphics.DrawPolygon(edge, silhouette);
            string title = Aircraft == null ? "SELECT AN AIRCRAFT" : Aircraft.Display.ToUpperInvariant();
            string meta = Aircraft == null ? "" : Aircraft.Nation.ToUpperInvariant() + "   •   RANK " + Roman(Aircraft.Rank);
            using (Font titleFont = new Font("Segoe UI Semibold", 12f))
            using (Font metaFont = new Font("Segoe UI", 8.5f))
            using (SolidBrush white = new SolidBrush(Theme.Text))
            using (SolidBrush muted = new SolidBrush(Theme.Muted))
            {
                e.Graphics.DrawString(title, titleFont, white, new RectangleF(12, rect.Height - 52, rect.Width - 24, 25));
                e.Graphics.DrawString(meta, metaFont, muted, new RectangleF(12, rect.Height - 28, rect.Width - 24, 20));
            }
        }

        private static string Roman(int rank)
        {
            string[] values = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return rank >= 0 && rank < values.Length ? values[rank] : rank.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly List<Aircraft> aircraft = new List<Aircraft>();
        private readonly List<TargetUnit> groundTargets = new List<TargetUnit>();
        private readonly List<TargetUnit> shipTargets = new List<TargetUnit>();
        private readonly List<DonorWeapon> nativeWeapons = new List<DonorWeapon>();
        private readonly List<DonorWeapon> globalWeapons = new List<DonorWeapon>();
        private readonly List<PylonSlot> pylons = new List<PylonSlot>();
        private readonly Dictionary<int, PylonAssignment> assignments = new Dictionary<int, PylonAssignment>();
        private readonly Dictionary<int, Button> pylonButtons = new Dictionary<int, Button>();

        private TextBox gameFolder;
        private TextBox aircraftSearch;
        private ComboBox nationFilter;
        private ComboBox rankFilter;
        private ListBox aircraftList;
        private AircraftPreview preview;
        private FlowLayoutPanel pylonStrip;
        private Label massLabel;
        private Label stationLabel;
        private CheckBox injectionToggle;
        private TextBox weaponSearch;
        private ComboBox categoryFilter;
        private ComboBox weaponNationFilter;
        private ComboBox sortFilter;
        private ListView weaponList;
        private ComboBox airTargetBox;
        private ComboBox groundTargetBox;
        private ComboBox shipTargetBox;
        private NumericUpDown airCount;
        private NumericUpDown groundCount;
        private NumericUpDown shipCount;
        private CheckBox hostileGround;
        private Label status;
        private PylonSlot selectedPylon;

        private const string MissionFolderRelative = @"UserMissions\Universal Test Lab";
        private const string StarterMissionName = "universal_test_lab_start.blk";

        public MainForm()
        {
            Text = "Universal Test Lab — Loadout Builder";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 760);
            Size = new Size(1440, 900);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.2f);
            LoadCatalogs();
            BuildUi();
            gameFolder.Text = DetectGameFolder();
            SelectDefaults();
        }

        private static string[] Lines(string resource)
        {
            return Embedded.Text(resource).Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        internal static double ParseNumber(string value)
        {
            double result;
            return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private void LoadCatalogs()
        {
            foreach (string line in Lines("UTL.aircraft.tsv"))
            {
                string[] p = line.Split('\t');
                int rank;
                if (p.Length >= 6 && Int32.TryParse(p[5], out rank))
                    aircraft.Add(new Aircraft { Id = p[0], Display = p[1], Type = p[2], DefaultPreset = p[3], Nation = p[4], Rank = rank, MaxLoad = p.Length > 6 ? ParseNumber(p[6]) : 0 });
            }
            foreach (string line in Lines("UTL.ground.tsv"))
            {
                string[] p = line.Split('\t');
                if (p.Length >= 3) groundTargets.Add(new TargetUnit { Id = p[0], Display = p[1], DefaultPreset = p[2] });
            }
            foreach (string line in Lines("UTL.ships.tsv"))
            {
                string[] p = line.Split('\t');
                if (p.Length >= 3) shipTargets.Add(new TargetUnit { Id = p[0], Display = p[1], DefaultPreset = p[2] });
            }
            foreach (string line in Lines("UTL.donor_weapons.tsv"))
            {
                string[] p = line.Split('\t');
                int slot, bullets;
                if (p.Length < 13 || !Int32.TryParse(p[2], out slot) || !Int32.TryParse(p[7], out bullets)) continue;
                nativeWeapons.Add(new DonorWeapon
                {
                    AircraftId = p[0], AircraftDisplay = p[1], Slot = slot, Mount = p[3], Trigger = p[4], Blk = p[5],
                    Emitter = p[6], Bullets = bullets, Icon = p[8], Name = p[9], Category = p[10], UnitMass = ParseNumber(p[11]), TotalMass = ParseNumber(p[12])
                });
            }
            foreach (string line in Lines("UTL.weapon_catalog.tsv"))
            {
                string[] p = line.Split('\t');
                int bullets;
                if (p.Length < 8 || !Int32.TryParse(p[2], out bullets)) continue;
                globalWeapons.Add(new DonorWeapon { Trigger = p[0], Blk = p[1], Bullets = bullets, Icon = p[3], Name = p[4], Category = p[5], UnitMass = ParseNumber(p[6]), TotalMass = ParseNumber(p[7]), Nations = p.Length > 8 ? p[8] : "" });
            }
            foreach (string line in Lines("UTL.aircraft_slots.tsv"))
            {
                string[] p = line.Split('\t');
                int slot, order, tier;
                if (p.Length < 6 || !Int32.TryParse(p[1], out slot) || !Int32.TryParse(p[2], out order) || !Int32.TryParse(p[3], out tier)) continue;
                pylons.Add(new PylonSlot { AircraftId = p[0], Slot = slot, Order = order, Tier = tier, MaxLoad = ParseNumber(p[4]), AnchorMount = p[5] });
            }
            PopulateWeaponNations();
        }

        private void PopulateWeaponNations()
        {
            Dictionary<string, string> aircraftNations = aircraft.GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Nation, StringComparer.OrdinalIgnoreCase);
            foreach (DonorWeapon weapon in nativeWeapons)
            {
                string nation;
                weapon.Nations = aircraftNations.TryGetValue(weapon.AircraftId, out nation) ? nation : "";
            }
            Dictionary<string, List<DonorWeapon>> sources = nativeWeapons
                .Where(w => w.AircraftId.IndexOf("killstreak", StringComparison.OrdinalIgnoreCase) < 0 && !w.AircraftId.StartsWith("nt_", StringComparison.OrdinalIgnoreCase))
                .GroupBy(WeaponKey, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (DonorWeapon weapon in globalWeapons)
            {
                List<DonorWeapon> donors;
                List<string> nations = sources.TryGetValue(WeaponKey(weapon), out donors)
                    ? donors.Select(w => w.Nations).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList()
                    : new List<string>();
                if (!String.IsNullOrWhiteSpace(weapon.Nations))
                    nations.AddRange(weapon.Nations.Split('|').Where(x => !String.IsNullOrWhiteSpace(x)));
                if (nations.Count == 0)
                {
                    string inferred = InferWeaponNation(weapon.Blk);
                    if (!String.IsNullOrEmpty(inferred)) nations.Add(inferred);
                }
                weapon.Nations = String.Join("|", nations.Distinct().OrderBy(x => x).ToArray());
            }
        }

        private static string WeaponKey(DonorWeapon weapon)
        {
            return (weapon.Trigger ?? "") + "|" + (weapon.Blk ?? "") + "|" + weapon.Bullets.ToString(CultureInfo.InvariantCulture);
        }

        private static string InferWeaponNation(string blk)
        {
            string file = Path.GetFileNameWithoutExtension(blk ?? "").ToLowerInvariant();
            if (file.StartsWith("us_") || file.StartsWith("aim_") || file.StartsWith("agm_") || file.StartsWith("gbu_")) return "USA";
            if (file.StartsWith("su_") || file.StartsWith("ussr_") || file.StartsWith("ru_") || file.StartsWith("r_") || file.StartsWith("kh_")) return "USSR";
            if (file.StartsWith("uk_") || file.StartsWith("gb_") || file.Contains("brimstone")) return "Britain";
            if (file.StartsWith("fr_") || file.Contains("magic") || file.Contains("mica")) return "France";
            if (file.StartsWith("de_") || file.StartsWith("ger_")) return "Germany";
            if (file.StartsWith("it_") || file.StartsWith("ita_")) return "Italy";
            if (file.StartsWith("jp_") || file.StartsWith("ja_")) return "Japan";
            if (file.StartsWith("cn_") || file.StartsWith("ch_")) return "China";
            if (file.StartsWith("il_") || file.StartsWith("isr_")) return "Israel";
            if (file.StartsWith("se_") || file.StartsWith("sw_")) return "Sweden";
            return "";
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Theme.Window };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            Controls.Add(root);
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildWorkspace(), 0, 1);
            status = new Label { Dock = DockStyle.Fill, Text = "Ready.", ForeColor = Theme.Muted, BackColor = Color.FromArgb(19, 24, 30), Padding = new Padding(14, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(status, 0, 2);
        }

        private Control BuildHeader()
        {
            TableLayoutPanel bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, BackColor = Color.FromArgb(19, 24, 30), Padding = new Padding(14, 10, 14, 10) };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            Label title = Theme.Label("UNIVERSAL TEST LAB", true);
            title.Font = new Font("Segoe UI Semibold", 15f);
            title.ForeColor = Theme.Accent;
            bar.Controls.Add(title, 0, 0);
            bar.Controls.Add(Theme.Label("GAME FOLDER", false), 1, 0);
            gameFolder = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            Theme.Input(gameFolder);
            bar.Controls.Add(gameFolder, 2, 0);
            Button browse = new Button { Text = "BROWSE", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(browse, false);
            browse.Click += delegate { BrowseFolder(); };
            bar.Controls.Add(browse, 3, 0);
            Button install = new Button { Text = "INSTALL BASE", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(install, false);
            install.Click += delegate { InstallClicked(); };
            bar.Controls.Add(install, 4, 0);
            Button open = new Button { Text = "OPEN MISSIONS", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(open, false);
            open.Click += delegate { OpenMissionFolder(); };
            bar.Controls.Add(open, 5, 0);
            Button presets = new Button { Text = "PRESETS", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(presets, false);
            presets.Click += delegate { ShowPresets(); };
            bar.Controls.Add(presets, 6, 0);
            Button about = new Button { Text = "ABOUT", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(about, false);
            about.Click += delegate { ShowAbout(); };
            bar.Controls.Add(about, 7, 0);
            return bar;
        }

        private Control BuildWorkspace()
        {
            TableLayoutPanel workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10), BackColor = Theme.Window };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
            workspace.Controls.Add(BuildAircraftBrowser(), 0, 0);
            workspace.Controls.Add(BuildLoadoutBuilder(), 1, 0);
            workspace.Controls.Add(BuildMissionPanel(), 2, 0);
            return workspace;
        }

        private Control SurfacePanel()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(4) };
        }

        private Control BuildAircraftBrowser()
        {
            Panel panel = (Panel)SurfacePanel();
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Surface };
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.Controls.Add(Theme.Label("AIRCRAFT BROWSER", true), 0, 0);
            grid.Controls.Add(Theme.Label("SEARCH", false), 0, 1);
            aircraftSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            Theme.Input(aircraftSearch);
            aircraftSearch.TextChanged += delegate { FilterAircraft(); };
            grid.Controls.Add(aircraftSearch, 0, 2);
            grid.Controls.Add(Theme.Label("NATION", false), 0, 3);
            TableLayoutPanel filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            nationFilter = DarkCombo();
            rankFilter = DarkCombo();
            nationFilter.SelectedIndexChanged += delegate { FilterAircraft(); };
            rankFilter.SelectedIndexChanged += delegate { FilterAircraft(); };
            filters.Controls.Add(nationFilter, 0, 0);
            filters.Controls.Add(rankFilter, 1, 0);
            grid.Controls.Add(filters, 0, 4);
            grid.Controls.Add(Theme.Label("AVAILABLE AIRCRAFT", false), 0, 5);
            Label countHint = Theme.Label("Nation and rank filters use the in-game research tree.", false);
            countHint.Font = new Font("Segoe UI", 8.2f);
            grid.Controls.Add(countHint, 0, 6);
            aircraftList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, BackColor = Theme.Surface2, ForeColor = Theme.Text };
            aircraftList.SelectedIndexChanged += delegate { AircraftChanged(); };
            grid.Controls.Add(aircraftList, 0, 7);
            panel.Controls.Add(grid);
            return panel;
        }

        private Control BuildLoadoutBuilder()
        {
            Panel panel = (Panel)SurfacePanel();
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Surface };
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            TableLayoutPanel heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            heading.Controls.Add(Theme.Label("CUSTOM SECONDARY WEAPONS", true), 0, 0);
            massLabel = Theme.Label("MASS: 0 kg", false);
            massLabel.TextAlign = ContentAlignment.MiddleRight;
            heading.Controls.Add(massLabel, 1, 0);
            grid.Controls.Add(heading, 0, 0);
            stationLabel = Theme.Label("Select an aircraft, then choose a station.", false);
            grid.Controls.Add(stationLabel, 0, 1);
            pylonStrip = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.FromArgb(29, 37, 46), Padding = new Padding(5) };
            grid.Controls.Add(pylonStrip, 0, 2);
            grid.Controls.Add(BuildWeaponFilters(), 0, 3);
            weaponList = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
                BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(29, 37, 46), ForeColor = Theme.Text
            };
            weaponList.Columns.Add("Weapon", 330);
            weaponList.Columns.Add("Type", 185);
            weaponList.Columns.Add("Ammo", 62, HorizontalAlignment.Center);
            weaponList.Columns.Add("Mass", 85, HorizontalAlignment.Right);
            weaponList.Columns.Add("Mode", 78, HorizontalAlignment.Center);
            weaponList.DoubleClick += delegate { AssignSelectedWeapon(); };
            grid.Controls.Add(weaponList, 0, 4);
            TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 6, 0, 0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            actions.Controls.Add(Theme.Label("Double-click a weapon to mount it.", false), 0, 0);
            Button clear = new Button { Text = "CLEAR STATION", Dock = DockStyle.Fill };
            Theme.Button(clear, false);
            clear.Click += delegate { ClearSelectedStation(); };
            actions.Controls.Add(clear, 1, 0);
            Button clearAll = new Button { Text = "CLEAR ALL", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(clearAll, false);
            clearAll.Click += delegate { assignments.Clear(); RefreshPylons(); };
            actions.Controls.Add(clearAll, 2, 0);
            Button add = new Button { Text = "MOUNT WEAPON", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            Theme.Button(add, true);
            add.Click += delegate { AssignSelectedWeapon(); };
            actions.Controls.Add(add, 3, 0);
            grid.Controls.Add(actions, 0, 5);
            panel.Controls.Add(grid);
            return panel;
        }

        private Control BuildWeaponFilters()
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 5, 0, 4) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            injectionToggle = new CheckBox { Text = " INJECTION — ALL", Dock = DockStyle.Fill, ForeColor = Theme.Accent, BackColor = Theme.Surface2, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat };
            injectionToggle.FlatAppearance.BorderColor = Theme.Border;
            injectionToggle.CheckedChanged += delegate { RefreshWeaponCatalog(); };
            row.Controls.Add(injectionToggle, 0, 0);
            weaponSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(7, 3, 7, 3) };
            Theme.Input(weaponSearch);
            weaponSearch.Text = "";
            weaponSearch.TextChanged += delegate { RefreshWeaponCatalog(); };
            row.Controls.Add(weaponSearch, 1, 0);
            categoryFilter = DarkCombo();
            categoryFilter.SelectedIndexChanged += delegate { RefreshWeaponCatalog(); };
            row.Controls.Add(categoryFilter, 2, 0);
            weaponNationFilter = DarkCombo();
            weaponNationFilter.SelectedIndexChanged += delegate { RefreshWeaponCatalog(); };
            row.Controls.Add(weaponNationFilter, 3, 0);
            sortFilter = DarkCombo();
            sortFilter.Items.AddRange(new object[] { "Mass: low to high", "Mass: high to low", "Name: A to Z" });
            sortFilter.SelectedIndexChanged += delegate { RefreshWeaponCatalog(); };
            row.Controls.Add(sortFilter, 4, 0);
            return row;
        }

        private Control BuildMissionPanel()
        {
            Panel panel = (Panel)SurfacePanel();
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 13, ColumnCount = 1, Padding = new Padding(12), BackColor = Theme.Surface };
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.Controls.Add(Theme.Label("MISSION SETUP", true), 0, 0);
            preview = new AircraftPreview { Dock = DockStyle.Fill };
            grid.Controls.Add(preview, 0, 1);
            grid.Controls.Add(Theme.Label("TARGETS", true), 0, 2);
            grid.Controls.Add(Theme.Label("AIR TARGET", false), 0, 3);
            airTargetBox = TargetRowCombo(aircraft.Cast<object>().ToList());
            airCount = CountBox(1);
            grid.Controls.Add(ComboAndCount(airTargetBox, airCount), 0, 4);
            grid.Controls.Add(Theme.Label("GROUND TARGET / ENEMY AIR DEFENCE", false), 0, 5);
            groundTargetBox = TargetRowCombo(groundTargets.Cast<object>().ToList());
            groundCount = CountBox(1);
            hostileGround = new CheckBox { Text = "HOSTILE", Dock = DockStyle.Fill, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Accent, BackColor = Theme.Surface2 };
            hostileGround.FlatAppearance.BorderColor = Theme.Border;
            grid.Controls.Add(ComboCountAndOption(groundTargetBox, groundCount, hostileGround), 0, 6);
            grid.Controls.Add(Theme.Label("NAVAL TARGET", false), 0, 7);
            shipTargetBox = TargetRowCombo(shipTargets.Cast<object>().ToList());
            shipCount = CountBox(1);
            grid.Controls.Add(ComboAndCount(shipTargetBox, shipCount), 0, 8);
            Label details = Theme.Label("FLIGHT PROFILE\r\n• 100% internal fuel\r\n• 1,100 km/h for modern jets\r\n• 700 km/h for early jets (Rank V or below)\r\n• 450 km/h for propeller aircraft\r\n• FPV drone uses a safe 100 km/h spawn\r\n• Ammunition restore every 10 seconds\r\n• No external fuel tanks unless mounted\r\n\r\nHOSTILE makes the selected ground unit actively engage you.\r\n☢ Nuclear weapons use their native in-game detonation.", false);
            details.Padding = new Padding(4, 12, 4, 4);
            grid.Controls.Add(details, 0, 9);
            Label hint = Theme.Label("Apply creates a new hot-load mission. Reopen User Missions in War Thunder; no game restart is required.", false);
            hint.ForeColor = Theme.Good;
            grid.Controls.Add(hint, 0, 10);
            Button apply = new Button { Text = "BUILD & APPLY MISSION", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 11f) };
            Theme.Button(apply, true);
            apply.Click += delegate { ApplyClicked(); };
            grid.Controls.Add(apply, 0, 11);
            Label version = Theme.Label("CUSTOM LOADOUT BUILDER  •  HOT LOAD", false);
            version.TextAlign = ContentAlignment.MiddleCenter;
            grid.Controls.Add(version, 0, 12);
            panel.Controls.Add(grid);
            return panel;
        }

        private ComboBox DarkCombo()
        {
            ComboBox box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface2, ForeColor = Theme.Text };
            return box;
        }

        private ComboBox TargetRowCombo(List<object> values)
        {
            ComboBox box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Flat, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems, BackColor = Theme.Surface2, ForeColor = Theme.Text };
            box.Items.AddRange(values.ToArray());
            return box;
        }

        private NumericUpDown CountBox(int value)
        {
            return new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 20, Value = value, BackColor = Theme.Surface2, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        }

        private Control ComboAndCount(ComboBox box, NumericUpDown count)
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            row.Controls.Add(box, 0, 0);
            row.Controls.Add(count, 1, 0);
            return row;
        }

        private Control ComboCountAndOption(ComboBox box, NumericUpDown count, CheckBox option)
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            row.Controls.Add(box, 0, 0);
            row.Controls.Add(count, 1, 0);
            row.Controls.Add(option, 2, 0);
            return row;
        }

        private void SelectDefaults()
        {
            nationFilter.Items.Add("All Nations");
            foreach (string nation in aircraft.Select(a => a.Nation).Distinct().OrderBy(x => x)) nationFilter.Items.Add(nation);
            rankFilter.Items.Add("All Ranks");
            for (int i = 1; i <= Math.Max(9, aircraft.Max(a => a.Rank)); i++) rankFilter.Items.Add("Rank " + i.ToString(CultureInfo.InvariantCulture));
            categoryFilter.Items.Add("All Weapon Types");
            foreach (string category in globalWeapons.Select(w => w.Category).Distinct().OrderBy(x => x)) categoryFilter.Items.Add(category);
            weaponNationFilter.Items.Add("All Nations");
            foreach (string nation in aircraft.Select(a => a.Nation).Distinct().OrderBy(x => x)) weaponNationFilter.Items.Add(nation);
            nationFilter.SelectedIndex = 0;
            rankFilter.SelectedIndex = 0;
            categoryFilter.SelectedIndex = 0;
            weaponNationFilter.SelectedIndex = 0;
            sortFilter.SelectedIndex = 0;
            FilterAircraft();
            Aircraft defaultAircraft = aircraftList.Items.Cast<object>().OfType<Aircraft>().FirstOrDefault(a => a.Id == "ef_2000_typhoon_aesa");
            if (defaultAircraft != null) aircraftList.SelectedItem = defaultAircraft;
            SelectComboById(airTargetBox, "j_10c");
            SelectComboById(groundTargetBox, "ussr_bmpt");
            SelectComboById(shipTargetBox, "jp_battleship_yamato");
        }

        private static void SelectComboById(ComboBox combo, string id)
        {
            foreach (object item in combo.Items)
            {
                Aircraft a = item as Aircraft;
                TargetUnit t = item as TargetUnit;
                if ((a != null && a.Id == id) || (t != null && t.Id == id)) { combo.SelectedItem = item; return; }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private Aircraft SelectedAircraft { get { return aircraftList.SelectedItem as Aircraft; } }

        private void FilterAircraft()
        {
            if (aircraftList == null || nationFilter == null || rankFilter == null) return;
            string keep = SelectedAircraft == null ? null : SelectedAircraft.Id;
            string search = aircraftSearch.Text.Trim();
            string nation = nationFilter.SelectedIndex <= 0 ? null : nationFilter.SelectedItem as string;
            int rank = rankFilter.SelectedIndex <= 0 ? 0 : rankFilter.SelectedIndex;
            IEnumerable<Aircraft> query = aircraft;
            if (!String.IsNullOrEmpty(search)) query = query.Where(a => a.Display.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || a.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(nation)) query = query.Where(a => a.Nation == nation);
            if (rank > 0) query = query.Where(a => a.Rank == rank);
            List<Aircraft> filtered = query.OrderByDescending(a => a.Rank).ThenBy(a => a.Display).ToList();
            aircraftList.BeginUpdate();
            aircraftList.Items.Clear();
            aircraftList.Items.AddRange(filtered.Cast<object>().ToArray());
            aircraftList.EndUpdate();
            Aircraft previous = filtered.FirstOrDefault(a => a.Id == keep);
            if (previous != null) aircraftList.SelectedItem = previous;
            else if (filtered.Count > 0) aircraftList.SelectedIndex = 0;
        }

        private void AircraftChanged()
        {
            Aircraft selected = SelectedAircraft;
            assignments.Clear();
            selectedPylon = null;
            preview.Aircraft = selected;
            preview.Invalidate();
            BuildPylonStrip();
        }

        private static bool IsFpvDrone(Aircraft item)
        {
            return item != null && item.Id.Equals("uav_inf_fpv_strike_drone", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool LooksLikeJetAircraft(string unitBlk)
        {
            string text = unitBlk ?? "";
            return Regex.IsMatch(text, @"(?i)(jet_(?:fighter|bomber)_metaparts|armor_jet_engine|(?:standard|afterburner|start)ExhaustFxType:t\s*=\s*""jet_)");
        }

        internal static int ResolveSpawnSpeed(Aircraft item, string unitBlk)
        {
            if (IsFpvDrone(item)) return 100;
            if (LooksLikeJetAircraft(unitBlk))
                return item != null && item.Rank > 0 && item.Rank <= 5 ? 700 : 1100;
            return 450;
        }

        internal static string ApplyPlayerSpawnSpeed(string mission, int speedKmh)
        {
            if (String.IsNullOrEmpty(mission)) throw new ArgumentException("Mission text is required.", "mission");
            if (speedKmh <= 0) throw new ArgumentOutOfRangeException("speedKmh");
            Regex marker = new Regex(@"(?m)^(\s*)speed:r=1100\s*$");
            if (!marker.IsMatch(mission)) throw new InvalidOperationException("Player spawn-speed markers are missing from the mission template.");
            return marker.Replace(mission, delegate(Match match)
            {
                return match.Groups[1].Value + "speed:r=" + speedKmh.ToString(CultureInfo.InvariantCulture);
            });
        }

        private void BuildPylonStrip()
        {
            pylonStrip.SuspendLayout();
            pylonStrip.Controls.Clear();
            pylonButtons.Clear();
            Aircraft selected = SelectedAircraft;
            if (selected != null)
            {
                foreach (PylonSlot pylon in pylons.Where(p => p.AircraftId == selected.Id).OrderBy(p => p.Order).ThenBy(p => p.Slot))
                {
                    Button button = new Button { Width = 103, Height = 78, Tag = pylon, Margin = new Padding(3), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.4f) };
                    Theme.Button(button, false);
                    button.Click += delegate(object sender, EventArgs e) { SelectPylon((PylonSlot)((Button)sender).Tag); };
                    pylonStrip.Controls.Add(button);
                    pylonButtons[pylon.Slot] = button;
                }
            }
            pylonStrip.ResumeLayout();
            if (pylonButtons.Count > 0) SelectPylon((PylonSlot)pylonButtons.Values.First().Tag);
            else
            {
                stationLabel.Text = IsFpvDrone(selected)
                    ? "FPV DRONE — no external pylons. Fly into the target to detonate the built-in HEAT warhead."
                    : (selected != null && selected.Id.StartsWith("nt_", StringComparison.OrdinalIgnoreCase)
                        ? "This Nuclear Escalation variant has a fixed event loadout. Choose the standard aircraft to edit its bomb-bay stations."
                        : "This aircraft has no editable weapon stations in the current catalog.");
                RefreshPylons();
                RefreshWeaponCatalog();
            }
        }

        private void SelectPylon(PylonSlot pylon)
        {
            selectedPylon = pylon;
            stationLabel.Text = "STATION " + pylon.Slot + " — choose a compatible weapon, or enable Injection for the full catalog.";
            RefreshPylons();
            RefreshWeaponCatalog();
        }

        private void RefreshPylons()
        {
            double total = 0;
            foreach (KeyValuePair<int, Button> pair in pylonButtons)
            {
                PylonAssignment assignment;
                bool has = assignments.TryGetValue(pair.Key, out assignment);
                string weapon = has ? ShortName(assignment.Weapon.Name, 18) : "EMPTY";
                pair.Value.Text = "STATION " + pair.Key + "\r\n" + weapon + (has && assignment.Injected ? "\r\nINJECTED" : "");
                pair.Value.BackColor = selectedPylon != null && selectedPylon.Slot == pair.Key ? Theme.AccentDark : (has ? Color.FromArgb(53, 82, 76) : Theme.Surface3);
                pair.Value.FlatAppearance.BorderColor = selectedPylon != null && selectedPylon.Slot == pair.Key ? Theme.Accent : Theme.Border;
                if (has) total += assignment.Weapon.TotalMass;
            }
            Aircraft selected = SelectedAircraft;
            string limit = selected != null && selected.MaxLoad > 0 ? " / " + selected.MaxLoad.ToString("0", CultureInfo.InvariantCulture) + " kg" : "";
            massLabel.Text = "MASS: " + total.ToString("0.0", CultureInfo.InvariantCulture) + " kg" + limit;
        }

        private static string ShortName(string value, int length)
        {
            if (String.IsNullOrEmpty(value)) return "WEAPON";
            string cleaned = Regex.Replace(value, @"\s+(air-to-air|air-to-ground|guided)?\s*(missile|missiles|bomb|bombs)$", "", RegexOptions.IgnoreCase).Trim();
            return cleaned.Length <= length ? cleaned.ToUpperInvariant() : cleaned.Substring(0, Math.Max(3, length - 1)).ToUpperInvariant() + "…";
        }

        private void RefreshWeaponCatalog()
        {
            if (weaponList == null) return;
            weaponList.BeginUpdate();
            weaponList.Items.Clear();
            weaponList.Groups.Clear();
            if (selectedPylon == null || SelectedAircraft == null) { weaponList.EndUpdate(); return; }
            IEnumerable<DonorWeapon> source = injectionToggle.Checked
                ? globalWeapons
                : nativeWeapons.Where(w => w.AircraftId == SelectedAircraft.Id && w.Slot == selectedPylon.Slot)
                    .GroupBy(w => w.Blk + "|" + w.Bullets).Select(g => g.First());
            string search = weaponSearch.Text.Trim();
            if (!String.IsNullOrEmpty(search)) source = source.Where(w => w.Name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || w.Category.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || w.Blk.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            string category = categoryFilter.SelectedIndex <= 0 ? null : categoryFilter.SelectedItem as string;
            if (!String.IsNullOrEmpty(category)) source = source.Where(w => w.Category == category);
            string weaponNation = weaponNationFilter.SelectedIndex <= 0 ? null : weaponNationFilter.SelectedItem as string;
            if (!String.IsNullOrEmpty(weaponNation)) source = source.Where(w => (w.Nations ?? "").Split('|').Any(n => n.Equals(weaponNation, StringComparison.OrdinalIgnoreCase)));
            if (sortFilter.SelectedIndex == 1) source = source.OrderByDescending(w => w.TotalMass).ThenBy(w => w.Name);
            else if (sortFilter.SelectedIndex == 2) source = source.OrderBy(w => w.Name).ThenBy(w => w.TotalMass);
            else source = source.OrderBy(w => w.TotalMass).ThenBy(w => w.Name);
            Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();
            foreach (DonorWeapon weapon in source.Take(5000))
            {
                ListViewGroup group;
                if (!groups.TryGetValue(weapon.Category, out group))
                {
                    group = new ListViewGroup(weapon.Category, HorizontalAlignment.Left);
                    groups[weapon.Category] = group;
                    weaponList.Groups.Add(group);
                }
                ListViewItem item = new ListViewItem(weapon.Name, group);
                item.SubItems.Add(weapon.Category);
                item.SubItems.Add(weapon.Bullets.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(weapon.TotalMass > 0 ? weapon.TotalMass.ToString("0.0", CultureInfo.InvariantCulture) + " kg" : "—");
                bool risky = injectionToggle.Checked && IsRiskyForSelectedPylon(weapon);
                item.SubItems.Add(risky ? "RISK" : (injectionToggle.Checked ? "INJECT" : "NATIVE"));
                item.Tag = weapon;
                if (weapon.Category == "Nuclear Weapons") item.ForeColor = Theme.Accent;
                else if (risky) item.ForeColor = Color.FromArgb(225, 142, 90);
                weaponList.Items.Add(item);
            }
            weaponList.EndUpdate();
        }

        private void AssignSelectedWeapon()
        {
            if (selectedPylon == null || weaponList.SelectedItems.Count == 0) return;
            DonorWeapon weapon = weaponList.SelectedItems[0].Tag as DonorWeapon;
            if (weapon == null) return;
            assignments[selectedPylon.Slot] = new PylonAssignment { Pylon = selectedPylon, Weapon = weapon, Injected = injectionToggle.Checked };
            RefreshPylons();
        }

        private bool IsRiskyForSelectedPylon(DonorWeapon weapon)
        {
            return selectedPylon != null && IsRiskyForPylon(selectedPylon, weapon);
        }

        private bool IsRiskyForPylon(PylonSlot pylon, DonorWeapon weapon)
        {
            if (pylon == null || weapon == null) return true;
            IEnumerable<DonorWeapon> native = nativeWeapons.Where(w => w.AircraftId == pylon.AircraftId && w.Slot == pylon.Slot);
            if (native.Any(w => String.Equals(w.Blk, weapon.Blk, StringComparison.OrdinalIgnoreCase) && w.Bullets == weapon.Bullets)) return false;
            if (!native.Any(w => String.Equals(w.Trigger, weapon.Trigger, StringComparison.OrdinalIgnoreCase))) return true;
            string path = (weapon.Blk ?? "").Replace('\\', '/').ToLowerInvariant();
            return path.Contains("/containers/") || path.Contains("/equipment/") || path.Contains("/payloadguns/");
        }

        private bool ConfirmRiskyLoadout()
        {
            Aircraft selected = SelectedAircraft;
            if (selected == null) return false;
            double total = assignments.Values.Sum(a => a.Weapon.TotalMass);
            if (selected.MaxLoad > 0 && total > selected.MaxLoad)
            {
                MessageBox.Show(this, "The configured weapon mass exceeds this aircraft's external load limit. Reduce the loadout before building the mission.", "Loadout limit exceeded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            List<PylonAssignment> risky = assignments.Values.Where(a => a.Injected && IsRiskyForPylon(a.Pylon, a.Weapon)).OrderBy(a => a.Pylon.Order).ToList();
            if (risky.Count == 0) return true;
            string stations = String.Join(Environment.NewLine, risky.Take(8).Select(a => "• Station " + a.Pylon.Slot + " — " + a.Weapon.Name).ToArray());
            if (risky.Count > 8) stations += Environment.NewLine + "• …and " + (risky.Count - 8).ToString(CultureInfo.InvariantCulture) + " more";
            string message = "The game has no native mount of this weapon type on the selected stations:" + Environment.NewLine + Environment.NewLine + stations + Environment.NewLine + Environment.NewLine +
                "These Frankenstein mounts may work, but a structurally incompatible pylon can prevent the mission from loading. Build anyway?";
            return MessageBox.Show(this, message, "Injection compatibility warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void ClearSelectedStation()
        {
            if (selectedPylon == null) return;
            assignments.Remove(selectedPylon.Slot);
            RefreshPylons();
        }

        private static string DetectGameFolder()
        {
            string applicationFolder = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] candidates =
            {
                SettingsStore.LoadGameFolder(),
                applicationFolder,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WarThunder"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\common\War Thunder"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"War Thunder")
            };
            foreach (string candidate in candidates)
                if (!String.IsNullOrWhiteSpace(candidate) && File.Exists(Path.Combine(candidate, "aces.vromfs.bin"))) return candidate;
            return applicationFolder;
        }

        private string ValidGameRoot()
        {
            string root = gameFolder.Text.Trim().Trim('"');
            if (!File.Exists(Path.Combine(root, "aces.vromfs.bin"))) throw new InvalidOperationException("The selected folder does not contain aces.vromfs.bin. Select the War Thunder root folder.");
            root = Path.GetFullPath(root);
            SettingsStore.SaveGameFolder(root);
            return root;
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the War Thunder root folder";
                dialog.SelectedPath = Directory.Exists(gameFolder.Text) ? gameFolder.Text : "";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    gameFolder.Text = dialog.SelectedPath;
                    SettingsStore.SaveGameFolder(dialog.SelectedPath);
                }
            }
        }

        private static void WriteBytes(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".new";
            File.WriteAllBytes(temp, data);
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                try { File.Replace(temp, path, backup, true); }
                catch { File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
        }

        private void InstallBase(string root, bool overwrite)
        {
            string mission = Path.Combine(root, MissionFolderRelative, StarterMissionName);
            if (overwrite || !File.Exists(mission)) WriteBytes(mission, Embedded.Bytes("UTL.universal_test_lab.blk"));
            WriteEmbeddedWhenNeeded(Path.Combine(root, MissionFolderRelative, "usr.csv"), "UTL.usr.csv", overwrite);
            string obsoleteLocalization = Path.Combine(root, MissionFolderRelative, "usr_universal_test_lab.csv");
            if (File.Exists(obsoleteLocalization))
            {
                try { File.Delete(obsoleteLocalization); }
                catch { }
            }
            WriteEmbeddedWhenNeeded(Path.Combine(root, @"content\pkg_user\levels\Clean_Testdrive.bin"), "UTL.Clean_Testdrive.bin", overwrite);
            WriteEmbeddedWhenNeeded(Path.Combine(root, @"content\pkg_user\levels\Clean_Testdrive.blk"), "UTL.Clean_Testdrive.blk", overwrite);
            WriteEmbeddedWhenNeeded(Path.Combine(root, @"content\pkg_user\levels\Clean_Testdrive_map.png"), "UTL.Clean_Testdrive_map.png", overwrite);
            CleanLegacyMissionMenus(root);
        }

        private static void CleanLegacyMissionMenus(string root)
        {
            string userMissions = Path.Combine(root, "UserMissions");
            if (!Directory.Exists(userMissions)) return;
            foreach (string path in Directory.GetFiles(userMissions, "*.blk", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }
                if (text.IndexOf("UniversalTestLab", StringComparison.Ordinal) < 0 &&
                    text.IndexOf("CleanTestDrive", StringComparison.Ordinal) < 0 &&
                    text.IndexOf("chapter:t=\"TestDrive\"", StringComparison.Ordinal) < 0 &&
                    text.IndexOf("name:t=\"universal_test_lab\"", StringComparison.Ordinal) < 0) continue;
                string cleaned = BlkTools.CleanLegacyMenuKeys(text);
                if (!cleaned.Equals(text, StringComparison.Ordinal)) WriteBytes(path, new UTF8Encoding(false).GetBytes(cleaned));
            }
        }

        private static void WriteEmbeddedWhenNeeded(string path, string resource, bool overwrite)
        {
            if (overwrite || !File.Exists(path)) WriteBytes(path, Embedded.Bytes(resource));
        }

        private void InstallClicked()
        {
            try
            {
                InstallBase(ValidGameRoot(), true);
                SetStatus("Base mission and clean test range installed.", false);
                MessageBox.Show(this, "Base mission installed. Close the User Missions tab in War Thunder and open it again; no game restart is required.", "Universal Test Lab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private static string ExtractResourceTool()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTestLab", "tools");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "wt_ext_cli.exe");
            WriteBytes(exe, Embedded.Bytes("UTL.wt_ext_cli.exe"));
            WriteBytes(Path.Combine(dir, "WT_EXT_LICENSE.txt"), Embedded.Bytes("UTL.WT_EXT_LICENSE.txt"));
            return exe;
        }

        private static string ExtractGameBlk(string root, string relative)
        {
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTestLab", "cache", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cache);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ExtractResourceTool(),
                Arguments = "unpack_vromf --input_dir_or_file \"" + Path.Combine(root, "aces.vromfs.bin") + "\" --output_dir \"" + cache + "\" --format BlkText --folder \"" + relative.Replace('\\', '/') + "\" --continue Quiet",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("Could not read game resource: " + relative + Environment.NewLine + output + Environment.NewLine + error);
            }
            string result = Path.Combine(cache, "aces.vromfs.bin_u", relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(result)) throw new FileNotFoundException("Extracted game resource was not found.", result);
            return result;
        }

        private GeneratedAircraft BuildCustomAircraft(string root, Aircraft target, string token)
        {
            string fm;
            if (IsFpvDrone(target))
            {
                string quad = File.ReadAllText(ExtractGameBlk(root, "gamedata/flightmodels/uav_quadcopter.blk"), Encoding.UTF8);
                string originalFpv = File.ReadAllText(ExtractGameBlk(root, "gamedata/flightmodels/uav_inf_fpv_strike_drone.blk"), Encoding.UTF8);
                fm = BuildDownloadedFpvVariant(quad, originalFpv);
            }
            else fm = File.ReadAllText(ExtractGameBlk(root, "gamedata/flightmodels/" + target.Id + ".blk"), Encoding.UTF8);
            int spawnSpeedKmh = ResolveSpawnSpeed(target, fm);
            if (!HasExplicitFlightModel(fm))
            {
                ExtractGameBlk(root, "gamedata/flightmodels/fm/" + target.Id + ".blk");
                EnsureExplicitFlightModel(ref fm, target.Id);
            }
            RemoveFuelTankPresets(ref fm);
            string classId = "utl_run_" + token + "_player";
            string presetId = "utl_run_" + token + "_loadout";
            StringBuilder loadout = new StringBuilder();
            foreach (PylonAssignment assignment in assignments.Values.OrderBy(a => a.Pylon.Order))
            {
                string mount;
                if (!assignment.Injected)
                {
                    mount = assignment.Weapon.Mount;
                    if (String.IsNullOrEmpty(mount)) throw new InvalidOperationException("Native mount information is missing for station " + assignment.Pylon.Slot + ".");
                }
                else
                {
                    // Keep the aircraft's native mount ID. The F2 pylon display is built from
                    // these registered station entries and ignores newly appended ad-hoc IDs.
                    mount = assignment.Pylon.AnchorMount;
                    string weaponBlk = PrepareInjectedWeapon(root, assignment.Weapon);
                    AddInjectedMount(ref fm, assignment.Pylon, assignment.Weapon, mount, weaponBlk);
                }
                loadout.AppendLine("Weapon {");
                loadout.AppendLine("\tslot:i = " + assignment.Pylon.Slot.ToString(CultureInfo.InvariantCulture));
                loadout.AppendLine("\tpreset:t = \"" + mount + "\"");
                loadout.AppendLine("}");
            }
            RegisterPreset(ref fm, presetId);
            string fmOut = Path.Combine(root, @"content\pkg_user\gameData\flightModels", classId + ".blk");
            string presetOut = Path.Combine(root, @"content\pkg_user\gameData\flightModels\weaponPresets", presetId + ".blk");
            WriteBytes(fmOut, new UTF8Encoding(false).GetBytes(fm));
            WriteBytes(presetOut, new UTF8Encoding(false).GetBytes(loadout.ToString()));
            return new GeneratedAircraft { ClassId = classId, PresetId = presetId, FlightModelPath = fmOut, PresetPath = presetOut, SpawnSpeedKmh = spawnSpeedKmh };
        }

        internal static bool HasExplicitFlightModel(string unitBlk)
        {
            return Regex.IsMatch(unitBlk ?? "", @"(?m)^\s*fmFile:t\s*=");
        }

        internal static void EnsureExplicitFlightModel(ref string unitBlk, string originalAircraftId)
        {
            if (HasExplicitFlightModel(unitBlk)) return;
            if (String.IsNullOrWhiteSpace(originalAircraftId)) throw new ArgumentException("Original aircraft ID is required.", "originalAircraftId");
            string cleanId = originalAircraftId.Trim().Replace('\\', '/').Trim('/');
            unitBlk = "fmFile:t = \"fm/" + cleanId + ".blk\"" + Environment.NewLine + (unitBlk ?? "");
        }

        internal static void RemoveFuelTankPresets(ref string fm)
        {
            List<BlockSpan> remove = new List<BlockSpan>();
            foreach (BlockSpan preset in BlkTools.Blocks(fm, "WeaponPreset"))
            {
                bool isFuelTank = BlkTools.Blocks(preset.Text, "Weapon").Any(weapon =>
                    String.Equals(BlkTools.Field(weapon.Text, "trigger", "t"), "fuel tanks", StringComparison.OrdinalIgnoreCase));
                if (isFuelTank) remove.Add(preset);
            }
            foreach (BlockSpan preset in remove.OrderByDescending(x => x.Start))
                fm = fm.Remove(preset.Start, preset.End - preset.Start + 1);
        }

        private static string PrepareInjectedWeapon(string root, DonorWeapon donor)
        {
            const string prefix = "utl-sam:";
            if (donor == null || String.IsNullOrEmpty(donor.Blk) || !donor.Blk.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return donor == null ? "" : donor.Blk;
            string descriptor = donor.Blk.Substring(prefix.Length);
            int separator = descriptor.LastIndexOf('#');
            if (separator <= 0 || separator >= descriptor.Length - 1) throw new InvalidOperationException("Ground SAM descriptor is invalid: " + donor.Blk);
            string sourceRelative = descriptor.Substring(0, separator);
            string bulletName = descriptor.Substring(separator + 1);
            string safeName = Regex.Replace(bulletName.ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');
            // The in-game selector localizes a rocket gun by its file basename. Preserve the
            // real missile ID and isolate the adapter in a subfolder to avoid overriding a
            // native aircraft rocket gun with the same name.
            string fileName = safeName + ".blk";
            string output = Path.Combine(root, @"content\pkg_user\gameData\Weapons\rocketGuns\utl_sam", fileName);
            string source = File.ReadAllText(ExtractGameBlk(root, sourceRelative), Encoding.UTF8);
            string adapter = BuildGroundSamAdapter(source, bulletName);
            WriteBytes(output, new UTF8Encoding(false).GetBytes(adapter));
            return "gameData/Weapons/rocketGuns/utl_sam/" + fileName;
        }

        internal static string BuildGroundSamAdapter(string source, string bulletName)
        {
            foreach (BlockSpan bullet in BlkTools.Blocks(source, "bullet"))
            {
                if (!String.Equals(BlkTools.Field(bullet.Text, "bulletName", "t"), bulletName, StringComparison.OrdinalIgnoreCase)) continue;
                BlockSpan rocket = BlkTools.FirstBlock(bullet.Text, "rocket", 0);
                if (rocket == null) continue;
                string rocketText = rocket.Text;
                int open = rocketText.IndexOf('{');
                string additions = Environment.NewLine + "\tbulletName:t = \"" + bulletName.Replace("\"", "") + "\"" + Environment.NewLine + "\ticonType:t = \"missile_type_b_air_to_air\"";
                rocketText = rocketText.Insert(open + 1, additions);
                // The ground IRIS-T SLM uses a deployed launcher animation. On an aircraft
                // that animation renders the whole launch container as the flying projectile.
                // Its static rocket mesh is valid, so use that without the launcher animation.
                if (bulletName.Equals("us_iris_t_sl", StringComparison.OrdinalIgnoreCase))
                {
                    rocketText = Regex.Replace(rocketText, @"(?m)^(\s*)mesh:t\s*=\s*""iris_t_sl_rocket""", "$1mesh:t = \"iris_t_rocket\"");
                    rocketText = Regex.Replace(rocketText, @"(?m)^\s*shellAnimChar:t\s*=\s*""[^""]*""\s*\r?\n", "");
                    int meshLine = rocketText.IndexOf("mesh:t = \"iris_t_rocket\"", StringComparison.Ordinal);
                    if (meshLine >= 0)
                    {
                        int lineEnd = rocketText.IndexOf('\n', meshLine);
                        if (lineEnd < 0) lineEnd = rocketText.Length;
                        rocketText = rocketText.Insert(lineEnd, Environment.NewLine + "\t\tshellAnimChar:t = \"iris_t_rocket_char\"");
                    }
                }
                string mesh = BlkTools.Field(rocketText, "mesh", "t") ?? "";
                StringBuilder adapter = new StringBuilder();
                adapter.AppendLine("rocketGun:b = true");
                adapter.AppendLine("bullets:i = 1");
                adapter.AppendLine("shotFreq:r = 1000.25");
                adapter.AppendLine("sound:t = \"weapon.rocketgun_132\"");
                if (!String.IsNullOrEmpty(mesh)) adapter.AppendLine("mesh:t = \"" + mesh + "\"");
                adapter.AppendLine("tags {");
                adapter.AppendLine("}");
                adapter.AppendLine(rocketText);
                return adapter.ToString();
            }
            throw new InvalidOperationException("Ground SAM missile was not found in its launcher file: " + bulletName);
        }

        internal static string BuildDownloadedFpvVariant(string quadcopter, string originalFpv)
        {
            BlockSpan warhead = BlkTools.FirstBlock(originalFpv, "warhead", 0);
            if (warhead == null) throw new InvalidOperationException("The FPV drone warhead definition is missing from the game resources.");
            int firstLine = quadcopter.IndexOf('\n');
            if (firstLine < 0) throw new InvalidOperationException("The installed UAV flight model is damaged.");
            string fpvProperties = @"
verifyEcsTemplate:b = false
useSimpleDeathConditionsAndEffects:b = false
humanDrone:b = true
drawFovOnTacticalMap:b = true
sceneCollisionTickStep:i = 2
overrideIndicatorIcon:t = ""iconKamikazeDrone""
hasFPVCamera:b = true
disableFPVHud:b = true
fpvCameraOffset:p3 = 0.2, -0.1, 0
";
            string result = quadcopter.Insert(firstLine + 1, fpvProperties);
            result += Environment.NewLine + warhead.Text + Environment.NewLine;
            return result;
        }

        internal static void AddInjectedMount(ref string fm, PylonSlot pylon, DonorWeapon donor, string mountId, string weaponBlk = null)
        {
            BlockSpan slotBlock = null;
            foreach (BlockSpan candidate in BlkTools.Blocks(fm, "WeaponSlot"))
            {
                Match index = Regex.Match(candidate.Text, @"index:i\s*=\s*(\d+)");
                if (index.Success && Int32.Parse(index.Groups[1].Value, CultureInfo.InvariantCulture) == pylon.Slot) { slotBlock = candidate; break; }
            }
            if (slotBlock == null) throw new InvalidOperationException("Aircraft station not found: " + pylon.Slot);
            BlockSpan anchor = null;
            foreach (BlockSpan candidate in BlkTools.Blocks(slotBlock.Text, "WeaponPreset"))
            {
                if (BlkTools.Field(candidate.Text, "name", "t") == pylon.AnchorMount) { anchor = candidate; break; }
            }
            if (anchor == null) throw new InvalidOperationException("Pylon anchor mount not found: " + pylon.AnchorMount);
            string emitter = null;
            foreach (BlockSpan weapon in BlkTools.Blocks(anchor.Text, "Weapon"))
            {
                emitter = BlkTools.Field(weapon.Text, "emitter", "t");
                if (!String.IsNullOrEmpty(emitter)) break;
            }
            if (String.IsNullOrEmpty(emitter)) throw new InvalidOperationException("Pylon emitter is missing for station " + pylon.Slot + ".");
            string replacement = anchor.Text;
            foreach (BlockSpan weapon in BlkTools.Blocks(replacement, "Weapon").OrderByDescending(x => x.Start))
                replacement = replacement.Remove(weapon.Start, weapon.End - weapon.Start + 1);
            replacement = Regex.Replace(replacement, @"(?m)^\s*showInWeaponMenu:b\s*=\s*(?:true|yes)\s*\r?\n", "");
            if (!String.IsNullOrEmpty(donor.Icon))
            {
                if (Regex.IsMatch(replacement, @"iconType:t\s*=")) replacement = BlkTools.ReplaceStringField(replacement, "iconType", donor.Icon);
                else
                {
                    int open = replacement.IndexOf('{');
                    replacement = replacement.Insert(open + 1, Environment.NewLine + "\t\t\ticonType:t = \"" + donor.Icon + "\"");
                }
            }
            if (String.Equals(donor.Trigger, "targetingPod", StringComparison.OrdinalIgnoreCase))
            {
                int open = replacement.IndexOf('{');
                string podProperties = Environment.NewLine + "\t\t\thasTargetingPod:b = true" + Environment.NewLine + "\t\t\tremoveGunnerOpticFps:i = 0";
                replacement = replacement.Insert(open + 1, podProperties);
            }
            StringBuilder weaponBlock = new StringBuilder();
            weaponBlock.AppendLine("\t\t\tWeapon {");
            weaponBlock.AppendLine("\t\t\t\ttrigger:t = \"" + donor.Trigger + "\"");
            weaponBlock.AppendLine("\t\t\t\tblk:t = \"" + (String.IsNullOrEmpty(weaponBlk) ? donor.Blk : weaponBlk) + "\"");
            weaponBlock.AppendLine("\t\t\t\temitter:t = \"" + emitter + "\"");
            weaponBlock.AppendLine("\t\t\t\texternal:b = true");
            weaponBlock.AppendLine("\t\t\t\tseparate:b = true");
            weaponBlock.AppendLine("\t\t\t\tbullets:i = " + Math.Max(1, donor.Bullets).ToString(CultureInfo.InvariantCulture));
            weaponBlock.Append("\t\t\t}");
            replacement = replacement.Insert(replacement.LastIndexOf('}'), Environment.NewLine + weaponBlock.ToString() + Environment.NewLine + "\t\t");
            int absoluteAnchorStart = slotBlock.Start + anchor.Start;
            fm = fm.Substring(0, absoluteAnchorStart) + replacement + fm.Substring(absoluteAnchorStart + anchor.Text.Length);
        }

        private static string CsvValue(string value)
        {
            return (value ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
        }

        private static void WriteMissionLocalization(string root, GeneratedAircraft generated, Aircraft source)
        {
            string display = CsvValue(source == null ? "Custom Aircraft" : source.Display);
            StringBuilder csv = new StringBuilder(Embedded.Text("UTL.usr.csv").TrimEnd());
            csv.AppendLine();
            csv.AppendLine("missions/universal_test_lab/date;Custom test session");
            csv.AppendLine("location/Clean_Testdrive;Clean Test Range");
            foreach (string key in new[] { generated.ClassId, generated.ClassId + "_0", generated.ClassId + "_1", generated.ClassId + "_2" })
                csv.AppendLine(key + ";" + display);
            WriteBytes(Path.Combine(root, MissionFolderRelative, "usr.csv"), new UTF8Encoding(false).GetBytes(csv.ToString()));
        }

        internal static void RegisterPreset(ref string fm, string presetId)
        {
            BlockSpan presets = BlkTools.FirstBlock(fm, "weapon_presets", 0);
            if (presets == null) throw new InvalidOperationException("Aircraft weapon_presets block is missing.");
            string registration = Environment.NewLine + "\tpreset {" + Environment.NewLine + "\t\tname:t = \"" + presetId + "\"" + Environment.NewLine + "\t\tblk:t = \"gameData/FlightModels/weaponPresets/" + presetId + ".blk\"" + Environment.NewLine + "\t}";
            fm = fm.Insert(presets.End, registration);
        }

        private void ApplyClicked()
        {
            try
            {
                Aircraft selected = SelectedAircraft;
                if (selected == null) throw new InvalidOperationException("Select an aircraft.");
                if (!ConfirmRiskyLoadout()) return;
                string root = ValidGameRoot();
                InstallBase(root, false);
                string token = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "_" + Process.GetCurrentProcess().Id;
                GeneratedAircraft generated = BuildCustomAircraft(root, selected, token);
                WriteMissionLocalization(root, generated, selected);
                Aircraft air = ResolveAircraft(airTargetBox);
                TargetUnit ground = ResolveTarget(groundTargetBox, groundTargets);
                TargetUnit ship = ResolveTarget(shipTargetBox, shipTargets);
                if (air == null || ground == null || ship == null) throw new InvalidOperationException("Check all target selections.");
                string text = Embedded.Text("UTL.universal_test_lab.blk");
                text = BlkTools.DisablePlayerSwitch(text);
                text = BlkTools.RemoveBotNotifications(text);
                text = ApplyPlayerSpawnSpeed(text, generated.SpawnSpeedKmh);
                text = BlkTools.UpdateUnit(text, "You", generated.ClassId, generated.PresetId, 1);
                text = BlkTools.UpdateUnit(text, "Target_Air_02", air.Id, air.DefaultPreset, (int)airCount.Value);
                text = BlkTools.UpdateUnit(text, "Target_03", ground.Id, ground.DefaultPreset, (int)groundCount.Value);
                text = BlkTools.UpdateUnit(text, "Ship_Target", ship.Id, ship.DefaultPreset, (int)shipCount.Value);
                if (hostileGround.Checked) text = BlkTools.MakeGroundTargetHostile(text, "Target_03");
                bool nuclear = assignments.Values.Any(a => a.Weapon.Category == "Nuclear Weapons");
                if (IsFpvDrone(selected)) text = BlkTools.AddFpvDetonationTriggers(text);
                string title = IsFpvDrone(selected)
                    ? "HOT UTL - FPV Strike Drone"
                    : "HOT UTL - " + selected.Display + " - Custom " + assignments.Count + " stations";
                if (title.Length > 150) title = title.Substring(0, 150);
                string description = IsFpvDrone(selected)
                    ? "Player-controlled FPV strike drone with local impact detonation."
                    : (nuclear ? "Custom hot-load aircraft with native nuclear weapons." : "Custom hot-load aircraft and pylon setup.");
                description += " Close and reopen the User Missions tab after applying.";
                text = BlkTools.UpdateMissionLabels(text, title, description);
                string missionDir = Path.Combine(root, MissionFolderRelative);
                Directory.CreateDirectory(missionDir);
                string missionPath = Path.Combine(missionDir, "universal_test_lab_hot_" + token + ".blk");
                WriteBytes(missionPath, new UTF8Encoding(false).GetBytes(text));
                CleanupPreviousGeneratedFiles(root, missionPath, generated);
                string refreshInstructions = "Mission generated successfully.\r\n\r\nIn War Thunder:\r\n1. Close the User Missions tab.\r\n2. Open User Missions again to refresh the mission list.\r\n3. Launch the current HOT UTL mission.";
                SetStatus("Mission generated. Close and reopen the User Missions tab in War Thunder to refresh it.", false);
                MessageBox.Show(this, refreshInstructions, "Mission generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private Aircraft ResolveAircraft(ComboBox combo)
        {
            Aircraft selected = combo.SelectedItem as Aircraft;
            if (selected != null) return selected;
            string value = combo.Text.Trim();
            return aircraft.FirstOrDefault(a => a.Id.Equals(value, StringComparison.OrdinalIgnoreCase) || a.Display.Equals(value, StringComparison.CurrentCultureIgnoreCase));
        }

        private static TargetUnit ResolveTarget(ComboBox combo, IEnumerable<TargetUnit> source)
        {
            TargetUnit selected = combo.SelectedItem as TargetUnit;
            if (selected != null) return selected;
            string value = combo.Text.Trim();
            return source.FirstOrDefault(t => t.Id.Equals(value, StringComparison.OrdinalIgnoreCase) || t.Display.Equals(value, StringComparison.CurrentCultureIgnoreCase));
        }

        private static void CleanupPreviousGeneratedFiles(string root, string currentMission, GeneratedAircraft current)
        {
            string missionDir = Path.Combine(root, MissionFolderRelative);
            foreach (string file in Directory.GetFiles(missionDir, "universal_test_lab*.blk"))
            {
                if (!Path.GetFullPath(file).Equals(Path.GetFullPath(currentMission), StringComparison.OrdinalIgnoreCase)) try { File.Delete(file); } catch { }
            }
            string fmDir = Path.Combine(root, @"content\pkg_user\gameData\flightModels");
            foreach (string file in Directory.GetFiles(fmDir, "utl_run_*_player.blk"))
            {
                if (!Path.GetFullPath(file).Equals(Path.GetFullPath(current.FlightModelPath), StringComparison.OrdinalIgnoreCase)) try { File.Delete(file); } catch { }
            }
            string presetDir = Path.Combine(fmDir, "weaponPresets");
            if (Directory.Exists(presetDir))
            {
                foreach (string file in Directory.GetFiles(presetDir, "utl_run_*_loadout.blk"))
                {
                    if (!Path.GetFullPath(file).Equals(Path.GetFullPath(current.PresetPath), StringComparison.OrdinalIgnoreCase)) try { File.Delete(file); } catch { }
                }
            }
        }

        private void OpenMissionFolder()
        {
            try
            {
                string path = Path.Combine(ValidGameRoot(), MissionFolderRelative);
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowPresets()
        {
            using (PresetManagerForm dialog = new PresetManagerForm(this)) dialog.ShowDialog(this);
        }

        private void ShowAbout()
        {
            using (AboutForm dialog = new AboutForm(aircraft.Count, globalWeapons.Count)) dialog.ShowDialog(this);
        }

        internal SavedPreset CaptureCurrentPreset(string name)
        {
            Aircraft selected = SelectedAircraft;
            if (selected == null) throw new InvalidOperationException("Select an aircraft before saving a preset.");
            SavedPreset preset = new SavedPreset { Name = name.Trim(), AircraftId = selected.Id };
            foreach (PylonAssignment assignment in assignments.Values.OrderBy(a => a.Pylon.Order))
            {
                DonorWeapon w = assignment.Weapon;
                preset.Entries.Add(new SavedPresetEntry
                {
                    Slot = assignment.Pylon.Slot, Injected = assignment.Injected, Mount = w.Mount, Trigger = w.Trigger, Blk = w.Blk,
                    Emitter = w.Emitter, Bullets = w.Bullets, Icon = w.Icon, Name = w.Name, Category = w.Category,
                    UnitMass = w.UnitMass, TotalMass = w.TotalMass
                });
            }
            return preset;
        }

        internal string CurrentAircraftName
        {
            get { return SelectedAircraft == null ? "Custom Loadout" : SelectedAircraft.Display + " Custom"; }
        }

        internal string AircraftName(string id)
        {
            Aircraft item = aircraft.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return item == null ? id : item.Display;
        }

        internal void LoadSavedPreset(SavedPreset preset)
        {
            Aircraft target = aircraft.FirstOrDefault(a => a.Id.Equals(preset.AircraftId, StringComparison.OrdinalIgnoreCase));
            if (target == null) throw new InvalidOperationException("The preset aircraft is not present in the current catalog: " + preset.AircraftId);
            aircraftSearch.Text = "";
            nationFilter.SelectedIndex = 0;
            rankFilter.SelectedIndex = 0;
            FilterAircraft();
            aircraftList.SelectedItem = aircraftList.Items.Cast<object>().OfType<Aircraft>().FirstOrDefault(a => a.Id == target.Id);
            assignments.Clear();
            int skipped = 0;
            foreach (SavedPresetEntry entry in preset.Entries)
            {
                PylonSlot pylon = pylons.FirstOrDefault(p => p.AircraftId == target.Id && p.Slot == entry.Slot);
                if (pylon == null) { skipped++; continue; }
                DonorWeapon weapon = entry.Injected
                    ? globalWeapons.FirstOrDefault(w => String.Equals(w.Blk, entry.Blk, StringComparison.OrdinalIgnoreCase) && String.Equals(w.Trigger, entry.Trigger, StringComparison.OrdinalIgnoreCase) && w.Bullets == entry.Bullets)
                    : nativeWeapons.FirstOrDefault(w => w.AircraftId == target.Id && w.Slot == entry.Slot && String.Equals(w.Mount, entry.Mount, StringComparison.OrdinalIgnoreCase) && String.Equals(w.Blk, entry.Blk, StringComparison.OrdinalIgnoreCase));
                if (weapon == null)
                {
                    weapon = new DonorWeapon
                    {
                        Mount = entry.Mount, Trigger = entry.Trigger, Blk = entry.Blk, Emitter = entry.Emitter, Bullets = entry.Bullets,
                        Icon = entry.Icon, Name = entry.Name, Category = entry.Category, UnitMass = entry.UnitMass, TotalMass = entry.TotalMass,
                        AircraftId = target.Id, AircraftDisplay = target.Display, Slot = entry.Slot, Nations = target.Nation
                    };
                }
                assignments[entry.Slot] = new PylonAssignment { Pylon = pylon, Weapon = weapon, Injected = entry.Injected };
            }
            BuildPylonStrip();
            RefreshPylons();
            SetStatus("Loaded preset: " + preset.Name + (skipped > 0 ? " (skipped " + skipped + " missing stations)" : ""), false);
        }

        private void SetStatus(string message, bool error)
        {
            status.Text = message;
            status.ForeColor = error ? Theme.Danger : Theme.Good;
        }

        private void ShowError(Exception ex)
        {
            SetStatus("Error: " + ex.Message, true);
            MessageBox.Show(this, ex.Message, "Universal Test Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal sealed class PresetManagerForm : Form
    {
        private readonly MainForm main;
        private readonly List<SavedPreset> presets;
        private readonly ListView list;
        private readonly TextBox presetName;

        public PresetManagerForm(MainForm owner)
        {
            main = owner;
            presets = PresetStore.Load();
            Text = "Custom Presets";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 500);
            MinimumSize = new Size(620, 420);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.2f);

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(16), BackColor = Theme.Window };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            Label heading = Theme.Label("CUSTOM LOADOUT PRESETS", true);
            heading.Font = new Font("Segoe UI Semibold", 15f);
            heading.ForeColor = Theme.Accent;
            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(Theme.Label("PRESET NAME", false), 0, 1);
            presetName = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Text = main.CurrentAircraftName };
            Theme.Input(presetName);
            root.Controls.Add(presetName, 0, 2);

            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text };
            list.Columns.Add("Preset", 360);
            list.Columns.Add("Aircraft", 260);
            list.DoubleClick += delegate { LoadSelected(); };
            root.Controls.Add(list, 0, 3);

            TableLayoutPanel buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 8, 0, 0) };
            for (int i = 0; i < 4; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            Button save = DialogButton("SAVE CURRENT", true); save.Click += delegate { SaveCurrent(); }; buttons.Controls.Add(save, 0, 0);
            Button load = DialogButton("LOAD SELECTED", false); load.Click += delegate { LoadSelected(); }; buttons.Controls.Add(load, 1, 0);
            Button delete = DialogButton("DELETE", false); delete.Click += delegate { DeleteSelected(); }; buttons.Controls.Add(delete, 2, 0);
            Button close = DialogButton("CLOSE", false); close.Click += delegate { Close(); }; buttons.Controls.Add(close, 3, 0);
            root.Controls.Add(buttons, 0, 4);
            RefreshList();
        }

        private Button DialogButton(string text, bool primary)
        {
            Button button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 0) };
            Theme.Button(button, primary);
            return button;
        }

        private SavedPreset Selected
        {
            get { return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as SavedPreset; }
        }

        private void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (SavedPreset preset in presets.OrderBy(x => x.Name))
            {
                ListViewItem row = new ListViewItem(preset.Name);
                row.SubItems.Add(main.AircraftName(preset.AircraftId));
                row.Tag = preset;
                list.Items.Add(row);
            }
            list.EndUpdate();
        }

        private void SaveCurrent()
        {
            try
            {
                string name = presetName.Text.Trim();
                if (String.IsNullOrEmpty(name)) throw new InvalidOperationException("Enter a preset name.");
                SavedPreset existing = presets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
                if (existing != null && MessageBox.Show(this, "Replace the existing preset named '" + existing.Name + "'?", "Replace preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                if (existing != null) presets.Remove(existing);
                presets.Add(main.CaptureCurrentPreset(name));
                PresetStore.Save(presets);
                RefreshList();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Custom Presets", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void LoadSelected()
        {
            try
            {
                SavedPreset selected = Selected;
                if (selected == null) throw new InvalidOperationException("Select a preset to load.");
                main.LoadSavedPreset(selected);
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Custom Presets", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DeleteSelected()
        {
            SavedPreset selected = Selected;
            if (selected == null) return;
            if (MessageBox.Show(this, "Delete preset '" + selected.Name + "'?", "Delete preset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            presets.Remove(selected);
            PresetStore.Save(presets);
            RefreshList();
        }
    }

    internal sealed class AboutForm : Form
    {
        private const string SupportUrl = "https://buy.stripe.com/bJe00bbHB0GH0qI655fQI00";
        private const string ProjectUrl = "https://github.com/UKRAngler/Universal-Test-Lab";
        private const string InspirationVideoUrl = "https://youtu.be/k0_Cz1ytgrQ?si=Sa5fDdDP8CatKawM";
        private const string Ask3ladUrl = "https://www.youtube.com/@Ask3lad";

        public AboutForm(int aircraftCount, int weaponCount)
        {
            Text = "About Universal Test Lab";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(860, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(24), BackColor = Theme.Window };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            Label title = Theme.Label("UNIVERSAL TEST LAB", true);
            title.Font = new Font("Segoe UI Semibold", 20f);
            title.ForeColor = Theme.Accent;
            root.Controls.Add(title, 0, 0);
            Label version = Theme.Label("Custom aircraft, target and pylon setup for War Thunder User Missions", false);
            version.Font = new Font("Segoe UI", 10.5f);
            root.Controls.Add(version, 0, 1);
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            root.Controls.Add(content, 0, 2);

            TableLayoutPanel info = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(0, 0, 18, 0) };
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            content.Controls.Add(info, 0, 0);

            Label body = Theme.Label(
                "CATALOG\r\n" + aircraftCount.ToString(CultureInfo.InvariantCulture) + " aircraft  •  " + weaponCount.ToString(CultureInfo.InvariantCulture) + " weapon entries\r\n\r\n" +
                "INSPIRED BY ASK3LAD\r\nThis project was inspired by Ask3lad's War Thunder custom-mission GUI. His project also provides GUI tools for ground and naval vehicles.\r\n\r\n" +
                "THIRD-PARTY COMPONENT\r\nIncludes wt_ext_cli for reading War Thunder resources. Its Apache 2.0 license is bundled with the application.", false);
            body.Font = new Font("Segoe UI", 10f);
            body.Padding = new Padding(8, 8, 8, 8);
            info.Controls.Add(body, 0, 0);

            Button inspiration = new Button { Text = "WATCH THE INSPIRATION VIDEO", Dock = DockStyle.Fill, Margin = new Padding(8, 3, 8, 3) };
            Theme.Button(inspiration, false);
            inspiration.Click += delegate { OpenUrl(InspirationVideoUrl); };
            info.Controls.Add(inspiration, 0, 1);

            Button channel = new Button { Text = "OPEN ASK3LAD CHANNEL", Dock = DockStyle.Fill, Margin = new Padding(8, 3, 8, 3) };
            Theme.Button(channel, false);
            channel.Click += delegate { OpenUrl(Ask3ladUrl); };
            info.Controls.Add(channel, 0, 2);

            Button project = new Button { Text = "OPEN PROJECT ON GITHUB", Dock = DockStyle.Fill, Margin = new Padding(8, 3, 8, 3) };
            Theme.Button(project, false);
            project.Click += delegate { OpenUrl(ProjectUrl); };
            info.Controls.Add(project, 0, 3);

            TableLayoutPanel support = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(16), BackColor = Theme.Surface };
            support.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            support.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            support.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            support.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            content.Controls.Add(support, 1, 0);

            Label supportTitle = Theme.Label("SUPPORT THE PROJECT", true);
            supportTitle.TextAlign = ContentAlignment.MiddleCenter;
            supportTitle.ForeColor = Theme.Accent;
            support.Controls.Add(supportTitle, 0, 0);

            PictureBox qr = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(12), Cursor = Cursors.Hand };
            qr.Image = LoadEmbeddedImage("UTL.support-qr.png");
            qr.Click += delegate { OpenUrl(SupportUrl); };
            support.Controls.Add(qr, 0, 1);

            Label supportText = Theme.Label("Scan the QR code or open the secure Stripe payment page. Support is optional.", false);
            supportText.TextAlign = ContentAlignment.MiddleCenter;
            support.Controls.Add(supportText, 0, 2);

            Button stripe = new Button { Text = "SUPPORT VIA STRIPE", Dock = DockStyle.Fill, Margin = new Padding(8, 3, 8, 3) };
            Theme.Button(stripe, true);
            stripe.Click += delegate { OpenUrl(SupportUrl); };
            support.Controls.Add(stripe, 0, 3);

            Label privacy = Theme.Label("Presets stay on this PC. The application does not send loadouts or account data anywhere.", false);
            privacy.ForeColor = Theme.Good;
            root.Controls.Add(privacy, 0, 3);
            Button close = new Button { Text = "CLOSE", Dock = DockStyle.Right, Width = 140 };
            Theme.Button(close, true);
            close.Click += delegate { Close(); };
            root.Controls.Add(close, 0, 4);
        }

        private static Image LoadEmbeddedImage(string resourceName)
        {
            using (MemoryStream stream = new MemoryStream(Embedded.Bytes(resourceName)))
            using (Image source = Image.FromStream(stream))
                return new Bitmap(source);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the link.\r\n\r\n" + url + "\r\n\r\n" + ex.Message, "Universal Test Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length >= 2 && args[0] == "--screenshot")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (MainForm form = new MainForm())
                {
                    form.Show();
                    Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                        bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    form.Close();
                }
                return;
            }
            if (args != null && args.Any(a => a == "--selftest"))
            {
                string text = Embedded.Text("UTL.universal_test_lab.blk");
                text = BlkTools.DisablePlayerSwitch(text);
                text = BlkTools.RemoveBotNotifications(text);
                text = BlkTools.UpdateUnit(text, "You", "utl_run_selftest_player", "utl_run_selftest_loadout", 1);
                string fpvMission = BlkTools.AddFpvDetonationTriggers(text);
                string hostileMission = BlkTools.MakeGroundTargetHostile(text, "Target_03");
                if (text.Count(c => c == '{') != text.Count(c => c == '}') ||
                    text.IndexOf("doNuclearExplosion", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("ID_FIRE_SECONDARY", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("campaign:t=\"UserMissions\"", StringComparison.Ordinal) < 0 ||
                    text.IndexOf("campaign:t=\"UniversalTestLab\"", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("chapter:t=\"TestDrive\"", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("Player Respawn Flight Profile", StringComparison.Ordinal) < 0 ||
                    text.IndexOf("Respawning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Rearmed", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("Mission self-test failed.");
                if (hostileMission.Count(c => c == '{') != hostileMission.Count(c => c == '}') ||
                    hostileMission.IndexOf("UTL Hostile Ground Target", StringComparison.Ordinal) < 0 ||
                    hostileMission.IndexOf("attack_type:t=\"fire_at_will\"", StringComparison.Ordinal) < 0 ||
                    hostileMission.IndexOf("object:t=\"Target_03\"", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("Hostile ground-target self-test failed.");
                if (fpvMission.Count(c => c == '{') != fpvMission.Count(c => c == '}') ||
                    fpvMission.IndexOf("UTL FPV Detonation - Target_03", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("effect:t=\"hit_81_132mm_heat\"", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("target:t=\"Target_03\"", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("math:t=\"3D\"", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("value:r=6", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("power:r=0.35", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("UTL FPV Re-arm Detonator", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("unitWhenRespawn", StringComparison.Ordinal) < 0 ||
                    fpvMission.IndexOf("doNuclearExplosion", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("FPV detonation self-test failed.");
                string legacyMenu = "  name:t=\"universal_test_lab\"\r\n  chapter:t=\"TestDrive\"\r\n  campaign:t=\"CleanTestDrive\"\r\n";
                string cleanMenu = BlkTools.CleanLegacyMenuKeys(legacyMenu);
                if (cleanMenu.IndexOf("campaign:t=\"UserMissions\"", StringComparison.Ordinal) < 0 ||
                    cleanMenu.IndexOf("CleanTestDrive", StringComparison.Ordinal) >= 0 ||
                    cleanMenu.IndexOf("TestDrive", StringComparison.Ordinal) >= 0 ||
                    cleanMenu.IndexOf("name:t=\"universal_test_lab\"", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("Legacy menu cleanup self-test failed.");
                if (Embedded.Text("UTL.aircraft.tsv").IndexOf("uav_inf_fpv_strike_drone\tFPV Strike Drone\t", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("FPV drone catalog self-test failed.");
                string aircraftData = Embedded.Text("UTL.aircraft.tsv");
                string stationData = Embedded.Text("UTL.aircraft_slots.tsv");
                if (aircraftData.IndexOf("nt_b_52h\tB-52H (Nuclear Escalation)\t", StringComparison.Ordinal) < 0 ||
                    aircraftData.IndexOf("nt_tu_95m\tTu-95M (Nuclear Escalation)\t", StringComparison.Ordinal) < 0 ||
                    stationData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(x => x.StartsWith("b_52h\t", StringComparison.Ordinal)) != 5 ||
                    stationData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(x => x.StartsWith("tu_95m\t", StringComparison.Ordinal)) != 1)
                    throw new InvalidOperationException("Strategic-bomber bomb-bay catalog self-test failed.");
                string fm = Embedded.Text("UTL.utl_safe_player.blk");
                PylonSlot pylon = new PylonSlot { Slot = 2, AnchorMount = "aim_120c_slot2_x2" };
                DonorWeapon weapon = new DonorWeapon { Trigger = "aam", Blk = "gameData/Weapons/rocketGuns/us_aim_120d.blk", Bullets = 1, Icon = "missile_type_c_air_to_air_midrange" };
                MainForm.AddInjectedMount(ref fm, pylon, weapon, "utl_run_selftest_slot_2");
                MainForm.RegisterPreset(ref fm, "utl_run_selftest_loadout");
                if (fm.Count(c => c == '{') != fm.Count(c => c == '}') ||
                    fm.IndexOf("us_aim_120d.blk", StringComparison.Ordinal) < 0 ||
                    fm.IndexOf("name:t = \"aim_120c_slot2_x2\"", StringComparison.Ordinal) < 0 ||
                    fm.IndexOf("name:t = \"utl_run_selftest_slot_2\"", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("Loadout/F2 replacement self-test failed.");
                string podFm = Embedded.Text("UTL.utl_safe_player.blk");
                DonorWeapon pod = new DonorWeapon { Trigger = "targetingPod", Blk = "gameData/Weapons/equipment/gr_litening_iii_targeting_pod.blk", Bullets = 1, Icon = "flir_container" };
                MainForm.AddInjectedMount(ref podFm, pylon, pod, "utl_run_selftest_pod_2");
                if (podFm.Count(c => c == '{') != podFm.Count(c => c == '}') ||
                    podFm.IndexOf("hasTargetingPod:b = true", StringComparison.Ordinal) < 0 ||
                    podFm.IndexOf("gr_litening_iii_targeting_pod.blk", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("Targeting-pod replacement self-test failed.");
                string tankCleanup = "WeaponSlot {\nindex:i=1\nWeaponPreset {\nname:t=\"ptb\"\nWeapon {\ntrigger:t=\"fuel tanks\"\nblk:t=\"drop_tank.blk\"\n}\n}\nWeaponPreset {\nname:t=\"aam\"\nWeapon {\ntrigger:t=\"aam\"\nblk:t=\"missile.blk\"\n}\n}\n}";
                MainForm.RemoveFuelTankPresets(ref tankCleanup);
                if (tankCleanup.IndexOf("fuel tanks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tankCleanup.IndexOf("missile.blk", StringComparison.Ordinal) < 0 ||
                    tankCleanup.Count(c => c == '{') != tankCleanup.Count(c => c == '}'))
                    throw new InvalidOperationException("Phantom fuel-tank cleanup self-test failed.");
                string legacyAircraft = "model:t = \"cw_21\"\nweapon_presets {\n}\n";
                MainForm.EnsureExplicitFlightModel(ref legacyAircraft, "cw_21");
                string modernAircraft = "model:t = \"modern\"\nfmFile:t = \"fm/modern.blk\"\n";
                MainForm.EnsureExplicitFlightModel(ref modernAircraft, "modern");
                if (legacyAircraft.IndexOf("fmFile:t = \"fm/cw_21.blk\"", StringComparison.Ordinal) < 0 ||
                    Regex.Matches(legacyAircraft, @"(?m)^\s*fmFile:t\s*=").Count != 1 ||
                    Regex.Matches(modernAircraft, @"(?m)^\s*fmFile:t\s*=").Count != 1 ||
                    modernAircraft.IndexOf("fm/modern.blk", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("Legacy aircraft flight-model reference self-test failed.");
                Aircraft propAircraft = new Aircraft { Id = "cw_21", Rank = 1 };
                Aircraft earlyJet = new Aircraft { Id = "f-80", Rank = 5 };
                Aircraft modernJet = new Aircraft { Id = "ef_2000_typhoon_aesa", Rank = 9 };
                string jetDefinition = "MetaPartsBlk:t = \"gameData/FlightModels/dm/metaparts/jet_fighter_metaparts.blk\"\nstandardExhaustFxType:t = \"jet_exhaust\"\n";
                if (MainForm.ResolveSpawnSpeed(propAircraft, legacyAircraft) != 450 ||
                    MainForm.ResolveSpawnSpeed(earlyJet, jetDefinition) != 700 ||
                    MainForm.ResolveSpawnSpeed(modernJet, jetDefinition) != 1100 ||
                    MainForm.ResolveSpawnSpeed(new Aircraft { Id = "uav_inf_fpv_strike_drone", Rank = 8 }, jetDefinition) != 100)
                    throw new InvalidOperationException("Aircraft spawn-speed profile self-test failed.");
                string earlyJetMission = MainForm.ApplyPlayerSpawnSpeed(Embedded.Text("UTL.universal_test_lab.blk"), 700);
                if (earlyJetMission.IndexOf("speed:r=1100", StringComparison.Ordinal) >= 0 ||
                    Regex.Matches(earlyJetMission, @"(?m)^\s*speed:r=700\s*$").Count != 4)
                    throw new InvalidOperationException("Mission spawn-speed replacement self-test failed.");
                string samSource = "bullet {\nbulletName:t=\"us_iris_t_sl\"\nbulletType:t=\"sam_tank\"\nrocket {\nmass:r=155\nmesh:t=\"iris_t_sl_rocket\"\nshellAnimChar:t=\"iris_t_sl_rocket_deployed_char\"\nguidance {\nuncageBeforeLaunch:b=true\n}\n}\n}";
                string samAdapter = MainForm.BuildGroundSamAdapter(samSource, "us_iris_t_sl");
                if (samAdapter.IndexOf("rocketGun:b = true", StringComparison.Ordinal) < 0 ||
                    samAdapter.IndexOf("bulletName:t = \"us_iris_t_sl\"", StringComparison.Ordinal) < 0 ||
                    samAdapter.IndexOf("uncageBeforeLaunch:b=true", StringComparison.Ordinal) < 0 ||
                    samAdapter.IndexOf("mesh:t = \"iris_t_rocket\"", StringComparison.Ordinal) < 0 ||
                    samAdapter.IndexOf("shellAnimChar:t = \"iris_t_rocket_char\"", StringComparison.Ordinal) < 0 ||
                    samAdapter.IndexOf("iris_t_sl_rocket_deployed_char", StringComparison.Ordinal) >= 0 ||
                    samAdapter.Count(c => c == '{') != samAdapter.Count(c => c == '}'))
                    throw new InvalidOperationException("Ground SAM adapter self-test failed.");
                string weaponCatalog = Embedded.Text("UTL.weapon_catalog.tsv");
                if (weaponCatalog.IndexOf("#us_aim_9x_block_2\t1\t", StringComparison.Ordinal) < 0 ||
                    weaponCatalog.IndexOf("\tGround SAM Missiles\t", StringComparison.Ordinal) < 0 ||
                    weaponCatalog.IndexOf("\tTargeting & Sensor Pods\t", StringComparison.Ordinal) < 0 ||
                    weaponCatalog.IndexOf("us_b28.blk", StringComparison.OrdinalIgnoreCase) < 0 ||
                    weaponCatalog.IndexOf("su_rds37.blk", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("Extended weapon catalog self-test failed.");
                string fpv = MainForm.BuildDownloadedFpvVariant("model:t = \"uav_quadcopter\"\nweapon_presets {\n}\n", "warhead {\n\tmass:r = 2.6\n}\n");
                if (fpv.IndexOf("model:t = \"uav_quadcopter\"", StringComparison.Ordinal) < 0 ||
                    fpv.IndexOf("humanDrone:b = true", StringComparison.Ordinal) < 0 ||
                    fpv.IndexOf("hasFPVCamera:b = true", StringComparison.Ordinal) < 0 ||
                    fpv.IndexOf("mass:r = 2.6", StringComparison.Ordinal) < 0 ||
                    fpv.Count(c => c == '{') != fpv.Count(c => c == '}'))
                    throw new InvalidOperationException("Downloaded FPV compatibility self-test failed.");
                Console.WriteLine("SELFTEST OK aircraft={0} weapons={1} native-nuclear=yes fpv-impact=yes clean-menu=yes f2-injected=yes pods=yes ground-sam=yes legacy-fm=yes adaptive-spawn=yes", LinesForTest("UTL.aircraft.tsv"), LinesForTest("UTL.weapon_catalog.tsv"));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static int LinesForTest(string resource)
        {
            return Embedded.Text(resource).Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
