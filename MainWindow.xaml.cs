using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SaintCoinach;
using SaintCoinach.Graphics;
using SaintCoinach.Graphics.Lgb;
using SaintCoinach.Imaging;
using SaintCoinach.Xiv;
using Brush = System.Drawing.Brush;
using Color = System.Drawing.Color;
using File = SaintCoinach.IO.File;
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
        private const string GameDirectory = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\";
        private ARealmReversed realm;

        private List<uint> currentIds;
        private Dictionary<TerritoryType, List<Point>> currentsMap;

        public MainWindow()
        {
            InitializeComponent();

            realm = new ARealmReversed(GameDirectory, SaintCoinach.Ex.Language.English);
            
            try
            {
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
            TerritoryType selected = (TerritoryType) PlaceBox.SelectedValue;
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
