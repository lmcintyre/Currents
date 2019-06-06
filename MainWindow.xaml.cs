using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Ookii.Dialogs.Wpf;
using SaintCoinach;
using SaintCoinach.Graphics.Lgb;
using SaintCoinach.Xiv;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Pen = System.Drawing.Pen;
using Point = System.Drawing.Point;

namespace Currents
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ARealmReversed realm;

        private List<uint> currentIds;
        private Dictionary<TerritoryType, List<Point>> currentsMap;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                string path = RequestGamePath();
                if (string.IsNullOrEmpty(path))
                    return;
                realm = new ARealmReversed(path, SaintCoinach.Ex.Language.English);

                currentsMap = new Dictionary<TerritoryType, List<Point>>();

                InitCurrentList();
                InitListBox();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        // Thanks Godbert.
        private static string RequestGamePath()
        {
            string path = Properties.Settings.Default.GamePath;
            if (!IsValidGamePath(path))
            {
                string programDir;
                if (Environment.Is64BitProcess)
                    programDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                else
                    programDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                path = System.IO.Path.Combine(programDir, "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn");

                if (IsValidGamePath(path))
                {
                    var msgResult = System.Windows.MessageBox.Show(string.Format("Found game installation at \"{0}\". Is this correct?", path), "Confirm game installation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                    if (msgResult == MessageBoxResult.Yes)
                    {
                        Properties.Settings.Default.GamePath = path;
                        Properties.Settings.Default.Save();

                        return path;
                    }

                    path = null;
                }
            }

            VistaFolderBrowserDialog dlg = null;

            while (!IsValidGamePath(path))
            {
                var result = (dlg ?? (dlg = new VistaFolderBrowserDialog
                {
                    Description = "Please select the directory of your FFXIV game installation (should contain 'boot' and 'game' directories).",
                    ShowNewFolderButton = false,
                })).ShowDialog();

                if (!result.GetValueOrDefault(false))
                {
                    var msgResult = System.Windows.MessageBox.Show("Cannot continue without a valid game installation, quit the program?", "That's no good", MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                    if (msgResult == MessageBoxResult.Yes)
                        return "";
                }

                path = dlg.SelectedPath;
            }

            Properties.Settings.Default.GamePath = path;
            Properties.Settings.Default.Save();
            return path;
        }

        private static bool IsValidGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!Directory.Exists(path))
                return false;

            return System.IO.File.Exists(System.IO.Path.Combine(path, "game", "ffxivgame.ver"));
        }

        private void InitListBox()
        {
            TerritoryType[] territoryTypes = realm.GameData.GetSheet<TerritoryType>().ToArray();
            List<TerritoryType> relevantTerritories = new List<TerritoryType>();

            foreach (TerritoryType t in territoryTypes)
            {
                if (!string.IsNullOrEmpty(t.PlaceName.ToString()))
                {
                    byte intendedUse = (byte)t.GetRaw("TerritoryIntendedUse");
                    if (intendedUse == 1)
                        if (!relevantTerritories.Select(_ => _.Bg).Contains(t.Bg) && ContainsCurrents(t))
                            relevantTerritories.Add(t);
                }
            }

            PlaceBox.ItemsSource = relevantTerritories;
            PlaceBox.DisplayMemberPath = "PlaceName";
            PlaceBox.SelectedValuePath = ".";
        }

        private void InitCurrentList()
        {
            currentIds = new List<uint>();
            var currentSheet = realm.GameData.GetSheet("EObjName");
            foreach (var row in currentSheet)
            {
                if (row?.AsString("Singular") == "aether current")
                    currentIds.Add((uint) row.Key);
            }
        }

        private void PlaceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string titleFormat = "Currents - {0} - {1} Field Currents";
            TerritoryType selected = (TerritoryType) PlaceBox.SelectedValue;
            Title = string.Format(titleFormat, selected.PlaceName, currentsMap[selected].Count);
            DrawPos(selected);
        }

        private bool ContainsCurrents(TerritoryType t)
        {
            const string pathFormat = "bg/{0}/planevent.lgb";

            string initPath = t.Bg;
            initPath = initPath.Substring(0, initPath.LastIndexOf('/'));

            string path = string.Format(pathFormat, initPath);
            if (!realm.GameData.PackCollection.TryGetFile(path, out var file))
                return false;

            LgbFile planEvent = new LgbFile(file);

            List<Point> theseCurrents = new List<Point>();
            foreach (LgbGroup group in planEvent.Groups)
                foreach (LgbEventObjectEntry entry in group.Entries.Where(_ => _?.Type == LgbEntryType.EventObject))
                    if (currentIds.Contains(entry.Header.EventObjectId))
                        theseCurrents.Add(new Point((int) entry.Header.Translation.X, (int) entry.Header.Translation.Z));

            if (theseCurrents.Count > 0)
            {
                currentsMap.Add(t, theseCurrents);
                return true;
            }
            return false;
        }

        private void DrawPos(TerritoryType teri)
        {
            Image mapImg = teri.Map.MediumImage;
            using (Graphics g = Graphics.FromImage(mapImg))
            {
                Pen p = new Pen(Color.Red, 30f);

                int origx = teri.Map.MediumImage.Width / 2;
                int origy = teri.Map.MediumImage.Height / 2;

                foreach (Point p1 in currentsMap[teri])
                    g.DrawEllipse(p, p1.X + origx - 3, p1.Y + origy - 3, 6, 6);

                g.Dispose();
            }

            BitmapImage bi = new BitmapImage();
            using (var ms = new MemoryStream())
            {
                mapImg.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
            }

            MapBox.Source = bi;
        }
    }
}
