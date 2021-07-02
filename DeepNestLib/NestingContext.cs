﻿namespace DeepNestLib
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using System.Xml.Linq;

  public class NestingContext
  {
    private readonly IMessageService messageService;

    public NestingContext(IMessageService messageService)
    {
      this.messageService = messageService;
    }

    public bool IsErrored { get; private set; }

    public List<NFP> Polygons { get; private set; } = new List<NFP>();

    public List<NFP> Sheets { get; private set; } = new List<NFP>();

    public double MaterialUtilization { get; private set; } = 0;

    public int PlacedPartsCount { get; private set; } = 0;

    SheetPlacement current = null;

    public SheetPlacement Current { get { return current; } }

    public SvgNest Nest { get; private set; }

    public Background Background { get; private set; }

    public int Iterations { get; private set; } = 0;

    public void StartNest()
    {
      this.current = null;
      Nest = new SvgNest(this.messageService, () => this.IsErrored = true);
      this.Background = new Background();
      Iterations = 0;
    }

    public void NestIterate()
    {
      try
      {
        List<NFP> lsheets = new List<NFP>();
        List<NFP> lpoly = new List<NFP>();

        for (int i = 0; i < Polygons.Count; i++)
        {
          Polygons[i].Id = i;
        }
        for (int i = 0; i < Sheets.Count; i++)
        {
          Sheets[i].Id = i;
        }
        foreach (var item in Polygons)
        {
          NFP clone = new NFP();
          clone.Id = item.Id;
          clone.Source = item.Source;
          clone.ReplacePoints(item.Points.Select(z => new SvgPoint(z.x, z.y) { exact = z.exact }));
          if (item.Children != null)
          {
            foreach (var citem in item.Children)
            {
              clone.Children.Add(new NFP());
              var l = clone.Children.Last();
              l.Id = citem.Id;
              l.Source = citem.Source;
              l.ReplacePoints(citem.Points.Select(z => new SvgPoint(z.x, z.y) { exact = z.exact }));
            }
          }

          lpoly.Add(clone);
        }

        foreach (var item in Sheets)
        {
          NFP clone = new NFP();
          clone.Id = item.Id;
          clone.Source = item.Source;
          clone.ReplacePoints(item.Points.Select(z => new SvgPoint(z.x, z.y) { exact = z.exact }));
          if (item.Children != null)
          {
            foreach (var citem in item.Children)
            {
              clone.Children.Add(new NFP());
              var l = clone.Children.Last();
              l.Id = citem.Id;
              l.Source = citem.Source;
              l.ReplacePoints(citem.Points.Select(z => new SvgPoint(z.x, z.y) { exact = z.exact }));
            }
          }

          lsheets.Add(clone);
        }

        if (SvgNest.Config.OffsetTreePhase)
        {
          var grps = lpoly.GroupBy(z => z.Source).ToArray();
          if (Background.UseParallel)
          {
            Parallel.ForEach(grps, (item) =>
            {
              SvgNest.OffsetTree(item.First(), 0.5 * SvgNest.Config.Spacing, SvgNest.Config);
              foreach (var zitem in item)
              {
                zitem.ReplacePoints(item.First().Points);
              }

            });

          }
          else
          {
            foreach (var item in grps)
            {
              SvgNest.OffsetTree(item.First(), 0.5 * SvgNest.Config.Spacing, SvgNest.Config);
              foreach (var zitem in item)
              {
                zitem.ReplacePoints(item.First().Points);
              }
            }
          }

          foreach (var item in lsheets)
          {
            var gap = SvgNest.Config.SheetSpacing - SvgNest.Config.Spacing / 2;
            SvgNest.OffsetTree(item, -gap, SvgNest.Config, true);
          }
        }

        List<NestItem> partsLocal = new List<NestItem>();
        var p1 = lpoly.GroupBy(z => z.Source).Select(z => new NestItem()
        {
          Polygon = z.First(),
          IsSheet = false,
          Quantity = z.Count()
        });

        var p2 = lsheets.GroupBy(z => z.Source).Select(z => new NestItem()
        {
          Polygon = z.First(),
          IsSheet = true,
          Quantity = z.Count()
        });


        partsLocal.AddRange(p1);
        partsLocal.AddRange(p2);
        int srcc = 0;
        foreach (var item in partsLocal)
        {
          item.Polygon.Source = srcc++;
        }

        Nest.launchWorkers(partsLocal.ToArray());
        var plcpr = Nest.nests.First();

        if (current == null || plcpr.fitness < current.fitness)
        {
          AssignPlacement(plcpr);
        }

        Iterations++;
      }
      catch (Exception ex)
      {
        if (!this.IsErrored)
        {
          this.IsErrored = true;
          this.messageService.DisplayMessage(ex);
        }
      }
    }

    public void AssignPlacement(SheetPlacement plcpr)
    {
      current = plcpr;
      double totalSheetsArea = 0;
      double totalPartsArea = 0;

      PlacedPartsCount = 0;
      List<NFP> placed = new List<NFP>();
      foreach (var item in Polygons)
      {
        item.Sheet = null;
      }
      List<int> sheetsIds = new List<int>();

      foreach (var item in plcpr.placements)
      {
        foreach (var zitem in item)
        {
          var sheetid = zitem.sheetId;
          if (!sheetsIds.Contains(sheetid))
          {
            sheetsIds.Add(sheetid);
          }

          var sheet = Sheets.First(z => z.Id == sheetid);
          totalSheetsArea += GeometryUtil.polygonArea(sheet);

          foreach (var ssitem in zitem.sheetplacements)
          {
            PlacedPartsCount++;
            var poly = Polygons.First(z => z.Id == ssitem.id);
            totalPartsArea += GeometryUtil.polygonArea(poly);
            placed.Add(poly);
            poly.Sheet = sheet;
            poly.x = ssitem.x + sheet.x;
            poly.y = ssitem.y + sheet.y;
            poly.Rotation = ssitem.rotation;
          }
        }
      }

      var emptySheets = Sheets.Where(z => !sheetsIds.Contains(z.Id)).ToArray();

      MaterialUtilization = Math.Abs(totalPartsArea / totalSheetsArea);

      var ppps = Polygons.Where(z => !placed.Contains(z));
      foreach (var item in ppps)
      {
        item.x = -1000;
        item.y = 0;
      }
    }

    public void ReorderSheets()
    {
      double x = 0;
      double y = 0;
      int gap = 10;
      for (int i = 0; i < Sheets.Count; i++)
      {
        Sheets[i].x = x;
        Sheets[i].y = y;
        if (Sheets[i] is Sheet)
        {
          var r = Sheets[i] as Sheet;
          x += r.Width + gap;
        }
        else
        {
          var maxx = Sheets[i].Points.Max(z => z.x);
          var minx = Sheets[i].Points.Min(z => z.x);
          var w = maxx - minx;
          x += w + gap;
        }
      }
    }

    public void AddSheet(int w, int h, int src)
    {
      var tt = new RectangleSheet();
      tt.Name = "sheet" + (Sheets.Count + 1);
      Sheets.Add(tt);

      tt.Source = src;
      tt.Height = h;
      tt.Width = w;
      tt.Rebuild();
      ReorderSheets();
    }

    Random r = new Random();

    public void LoadSampleData()
    {
      Console.WriteLine("Adding sheets..");
      //add sheets
      for (int i = 0; i < 5; i++)
      {
        AddSheet(3000, 1500, 0);
      }

      Console.WriteLine("Adding parts..");
      //add parts
      int src1 = GetNextSource();
      for (int i = 0; i < 200; i++)
      {
        AddRectanglePart(src1, 250, 220);
      }
    }

    public void LoadInputData(string path, int count)
    {
      var dir = new DirectoryInfo(path);
      foreach (var item in dir.GetFiles("*.svg"))
      {
        try
        {
          var src = GetNextSource();
          for (int i = 0; i < count; i++)
          {
            TryImportFromRawDetail(SvgParser.LoadSvg(item.FullName), src, out _);
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine("Error loading " + item.FullName + ". skip");
        }
      }
    }

    public bool TryImportFromRawDetail(RawDetail raw, int src, out NFP loadedNfp)
    {
      loadedNfp = raw.ToNfp();
      if (loadedNfp == null)
      {
        return false;
      }

      loadedNfp.Source = src;
      Polygons.Add(loadedNfp);
      return true;
    }

    public int GetNextSource()
    {
      if (Polygons.Any())
      {
        return Polygons.Max(z => z.Source) + 1;
      }
      return 0;
    }

    public int GetNextSheetSource()
    {
      if (Sheets.Any())
      {
        return Sheets.Max(z => z.Source) + 1;
      }
      return 0;
    }

    public void AddRectanglePart(int src, int ww = 50, int hh = 80)
    {
      int xx = 0;
      int yy = 0;
      NFP pl = new NFP();

      Polygons.Add(pl);
      pl.Source = src;
      pl.AddPoint(new SvgPoint(xx, yy));
      pl.AddPoint(new SvgPoint(xx + ww, yy));
      pl.AddPoint(new SvgPoint(xx + ww, yy + hh));
      pl.AddPoint(new SvgPoint(xx, yy + hh));
    }

    public void LoadXml(string v)
    {
      var d = XDocument.Load(v);
      var f = d.Descendants().First();
      var gap = int.Parse(f.Attribute("gap").Value);
      SvgNest.Config.Spacing = gap;

      foreach (var item in d.Descendants("sheet"))
      {
        int src = GetNextSheetSource();
        var cnt = int.Parse(item.Attribute("count").Value);
        var ww = int.Parse(item.Attribute("width").Value);
        var hh = int.Parse(item.Attribute("height").Value);

        for (int i = 0; i < cnt; i++)
        {
          AddSheet(ww, hh, src);
        }
      }
      foreach (var item in d.Descendants("part"))
      {
        var cnt = int.Parse(item.Attribute("count").Value);
        var path = item.Attribute("path").Value;
        RawDetail r = null;
        if (path.ToLower().EndsWith("svg"))
        {
          r = SvgParser.LoadSvg(path);
        }
        else if (path.ToLower().EndsWith("dxf"))
        {
          r = DxfParser.LoadDxf(path);
        }
        else
        {
          continue;
        }

        var src = GetNextSource();

        for (int i = 0; i < cnt; i++)
        {
          TryImportFromRawDetail(r, src, out _);
        }
      }
    }
  }
}
