using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>A 3x3 movement template. Cells use the 1..9 numbering from the design document.</summary>
    public sealed class PatternTemplate
    {
        public string Name { get; }
        public int[] Cells { get; }

        public PatternTemplate(string name, params int[] cells)
        {
            Name = name;
            Cells = cells;
        }
    }

    public static class PatternLibrary
    {
        // 1 2 3
        // 4 5 6
        // 7 8 9
        public static readonly PatternTemplate[] Templates =
        {
            new PatternTemplate("Vaiven horizontal", 1, 2, 3, 2),
            new PatternTemplate("Vaiven vertical", 1, 4, 7, 4),
            new PatternTemplate("Recorrido en L", 1, 2, 3, 6, 9, 6, 3, 2),
            new PatternTemplate("Perimetro", 1, 2, 3, 6, 9, 8, 7, 4),
            new PatternTemplate("Serpiente", 1, 2, 3, 6, 5, 4, 7, 8, 9, 6, 5, 4),
            new PatternTemplate("Circuito doble", 1, 2, 5, 4, 7, 8, 5, 6, 3, 2)
        };

        public static GridPos CellToLocal(int cell)
        {
            if (cell < 1 || cell > 9) throw new ArgumentOutOfRangeException(nameof(cell));
            int index = cell - 1;
            int x = index % 3;
            int yFromTop = index / 3;
            return new GridPos(x, 2 - yFromTop);
        }

        public static GridPos[] TransformCells(int[] cells, int rotationDegrees, bool mirrorHorizontal, bool mirrorVertical)
        {
            int rotation = ((rotationDegrees % 360) + 360) % 360;
            if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270.", nameof(rotationDegrees));

            var result = new GridPos[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var p = CellToLocal(cells[i]);

                switch (rotation)
                {
                    case 90:
                        p = new GridPos(p.Y, 2 - p.X);
                        break;
                    case 180:
                        p = new GridPos(2 - p.X, 2 - p.Y);
                        break;
                    case 270:
                        p = new GridPos(2 - p.Y, p.X);
                        break;
                }

                if (mirrorHorizontal) p = new GridPos(2 - p.X, p.Y);
                if (mirrorVertical) p = new GridPos(p.X, 2 - p.Y);

                result[i] = p;
            }

            return result;
        }

        public static bool ValidateTemplate(int[] cells, out string error)
        {
            error = null;
            if (cells == null || cells.Length < 2)
            {
                error = "pattern needs at least two cells";
                return false;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] < 1 || cells[i] > 9)
                {
                    error = "cell outside 1..9";
                    return false;
                }
                if (!seen.Add(cells[i]))
                {
                    // Repeated cells are allowed in some templates (e.g. circuit double),
                    // but consecutive duplicates would create a zero-length move.
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                var a = CellToLocal(cells[i]);
                var b = CellToLocal(cells[(i + 1) % cells.Length]);
                if (!a.IsOrthogonallyAdjacent(b))
                {
                    error = $"transition {i} is not orthogonal";
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class PatternPlacement
    {
        public string TemplateName;
        public int RotationDegrees;
        public bool MirrorHorizontal;
        public bool MirrorVertical;
        public int Phase;
        public GridPos RegionOrigin;
        public GridPos[] Route;
        public GridPos[] LocalCells;

        public CyclicRoutePattern ToPattern() => new CyclicRoutePattern(Route);
    }

    /// <summary>
    /// Builds deterministic 3x3-derived cyclic routes. Templates are data; transforms and
    /// placements are validated before they are accepted.
    /// </summary>
    public static class GeneradorPatronMovimiento
    {
        public static PatternPlacement Generate(int width, int height, SeededRandom rng, int maxPlacementAttempts = 32)
        {
            if (width < 3 || height < 3) return null;

            var templateOrder = new List<int>();
            for (int i = 0; i < PatternLibrary.Templates.Length; i++) templateOrder.Add(i);
            rng.Shuffle(templateOrder);

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                var template = PatternLibrary.Templates[templateOrder[attempt % templateOrder.Count]];
                int rotation = rng.Pick(new[] { 0, 90, 180, 270 });
                bool mirrorH = rng.Chance(0.5);
                bool mirrorV = rng.Chance(0.5);
                int phase = rng.Next(template.Cells.Length);
                // Keep the patrol region one cell away from the outer border. This leaves room
                // for the inherited playback, which continues past the contact cell, and keeps
                // hazards eligible for the interior-only rule.
                int originX = rng.Range(1, Math.Max(1, width - 3));
                int originY = rng.Range(1, Math.Max(1, height - 3));

                var placement = BuildPlacement(template, rotation, mirrorH, mirrorV, phase, new GridPos(originX, originY), width, height);
                if (placement != null) return placement;
            }

            return null;
        }

        public static PatternPlacement BuildPlacement(
            PatternTemplate template,
            int rotationDegrees,
            bool mirrorHorizontal,
            bool mirrorVertical,
            int phase,
            GridPos regionOrigin,
            int width,
            int height)
        {
            if (template == null) return null;
            if (!PatternLibrary.ValidateTemplate(template.Cells, out _)) return null;
            if (regionOrigin.X < 0 || regionOrigin.Y < 0 || regionOrigin.X + 2 >= width || regionOrigin.Y + 2 >= height) return null;

            var local = PatternLibrary.TransformCells(template.Cells, rotationDegrees, mirrorHorizontal, mirrorVertical);
            int length = local.Length;
            int normalizedPhase = ((phase % length) + length) % length;

            var route = new GridPos[length];
            for (int i = 0; i < length; i++)
            {
                var localCell = local[(normalizedPhase + i) % length];
                var world = new GridPos(regionOrigin.X + localCell.X, regionOrigin.Y + localCell.Y);
                if (world.X < 0 || world.Y < 0 || world.X >= width || world.Y >= height) return null;
                route[i] = world;
            }

            if (!CyclicRoutePattern.IsValidRoute(route, out _)) return null;

            return new PatternPlacement
            {
                TemplateName = template.Name,
                RotationDegrees = rotationDegrees,
                MirrorHorizontal = mirrorHorizontal,
                MirrorVertical = mirrorVertical,
                Phase = normalizedPhase,
                RegionOrigin = regionOrigin,
                Route = route,
                LocalCells = local
            };
        }
    }
}
