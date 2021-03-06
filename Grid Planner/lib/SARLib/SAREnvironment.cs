﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace SARLib.SAREnvironment
{
    /// <summary>
    /// Schema for a generic two-dimensional point
    /// </summary>
    public interface IPoint
    {
        int X { get; set; }
        int Y { get; set; }
    }
    public class SARPoint : IPoint
    {        
        private PointTypes _type;
        private double _dangerLevel;
        private double _confidenceLevel;
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// Costruttore usato da serializzatore JSON
        /// </summary>
        public SARPoint()
        { }
        internal SARPoint(int x, int y, double confidence, double danger, PointTypes type)
        {
            X = x;
            Y = y;
            Type = type;
            Danger = danger;
            Confidence = confidence;
        }

        //[JsonProperty("Type")]
        //[JsonConverter(typeof(StringEnumConverter))]
        public PointTypes Type
        {
            get
            {
                return _type;
            }
            set
            {
                //togliere if
                if (value == PointTypes.Clear || value == PointTypes.Obstacle || value == PointTypes.Target)
                {
                    switch (value)
                    {
                        case PointTypes.Obstacle:
                            Danger = 1;
                            Confidence = 0;
                            break;
                        case PointTypes.Target:
                            //Confidence = 1;                            
                            break;
                        case PointTypes.Clear:
                            break;
                        default:
                            break;
                    }
                    _type = value;
                }
            }
        }

        public double Danger
        {
            get
            {
                return _dangerLevel;
            }
            set
            {
                if (0 <= value && value <= 1)
                {
                    _dangerLevel = value;
                }
            }
        }
        public double Confidence
        {
            get
            {
                return _confidenceLevel;
            }
            set
            {
                if (0 <= value && value <= 1)
                {
                    _confidenceLevel = value;
                }
            }
        }

        public enum PointTypes { Obstacle, Target, Clear }
        
        public String PrintConsoleFriendly()
        {
            switch (Type)
            {
                case PointTypes.Obstacle:
                    return "#";
                //break;
                case PointTypes.Target:
                    return "$";
                //break;                
                default:
                    return "%";
                    //break;
            }
        }
    }

    /// <summary>
    /// Schema for a generic two-dimensional grid
    /// </summary>
    public interface IGrid
    {
        int Distance(IPoint p1, IPoint p2);
        SARPoint[] GetNeighbors(IPoint point);
        IPoint GetPoint(int x, int y);
        String SaveToFile(string destinationPath, string fileName = null);
    }
    public class SARGrid : IGrid
    {
        //  row = Y
        //  ^
        //  |
        //  |
        //  |
        //  ----------> col = X

        //{ a a a a a a }
        //{ a b b b b a }
        //{ a a c c c c }

        //rivedere nomi e modificatori di accesso
        public int _numCol, _numRow;
        //rappresenta sia la topografia dell'ambiente che la distribuzione di probabilità degli obiettivi
        public SARPoint[,] _grid;
        
        //rappresenta le posizioni stimate dei target
        public List<SARPoint> _estimatedTargetPositions = new List<SARPoint>();

        //rappresenta la reale posizione del target
        public SARPoint _realTarget = null;

        #region Costruttori
        /// <summary>
        /// Costruttore default usato da serializzatore JSON
        /// </summary>
        public SARGrid()
        { }

        public SARGrid(int _numCol, int _numRow)
        {
            this._numCol = Math.Abs(_numCol);
            this._numRow = Math.Abs(_numRow);
            _grid = new SARPoint[this._numCol, this._numRow]; //colonna X riga

            for (int col = 0; col < this._numCol; col++)
            {
                for (int row = 0; row < this._numRow; row++)
                {
                    _grid[col, row] = BuildSARPoint(col, row, 0, 0, SARPoint.PointTypes.Clear);
                }
            }
        }

        /// <summary>
        /// Creazione dell'ambiente da file di configurazione
        /// </summary>
        /// <param name="sourceFile"></param>
        public SARGrid(string sourceFile)
        {
            var grid = LoadFromFile(sourceFile);

            _grid = grid._grid;
            _numCol = grid._numCol;
            _numRow = grid._numRow;
            _estimatedTargetPositions = GetPossibleTargetPositions();
            _realTarget = grid._realTarget;
        }
        #endregion

        private bool IsValidPoint(IPoint p)
        {
            return (0 <= p.X && p.X < _numCol) && (0 <= p.Y && p.Y < _numRow);
        }

        public int Distance(IPoint p1, IPoint p2)
        {
            if (IsValidPoint(p1) && IsValidPoint(p2))
            {
                return (Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y));
            }
            throw new IndexOutOfRangeException("Invalid Points");
        }

        public SARPoint[] GetNeighbors(IPoint p)
        {
            if (IsValidPoint(p))
            {
                List<SARPoint> neighbors = new List<SARPoint>
                {
                    GetPoint(p.X + 1,p.Y),
                    GetPoint(p.X - 1,p.Y),
                    GetPoint(p.X,p.Y + 1),
                    GetPoint(p.X,p.Y - 1)
                };
                return neighbors.FindAll(x => x != null && x.Type != SARPoint.PointTypes.Obstacle).ToArray();
            }
            return Array.Empty<SARPoint>();
        }

        public SARPoint BuildSARPoint(int x, int y, double confidence, double danger, SARPoint.PointTypes type)
        {
            //instanzio punto
            var point = new SARPoint(x, y, confidence, danger, type);

            //applicazione vincoli
            switch (point.Type)
            {
                case SARPoint.PointTypes.Obstacle:
                    point.Danger = 0;
                    point.Confidence = 0;
                    break;
                case SARPoint.PointTypes.Target:
                    break;
                case SARPoint.PointTypes.Clear:
                    //point.Confidence = 0;
                    break;
                default:
                    break;
            }
            
            //aggiungo il punto alla griglia
            _grid[x, y] = point;
            
            //propagazione dei valori di confidenza all'intorno
            var neighbors = this.GetNeighbors(point);
            foreach (var n in neighbors)
            {                
                var p = GetPoint(n.X, n.Y);
                if (p.Type == SARPoint.PointTypes.Clear)
                {
                    p.Danger = point.Danger * 0.5;
                    p.Confidence = point.Confidence * 0.5;
                }
            }

            return point;
        }

        public SARPoint GetPoint(int x, int y)
        {
            if (IsValidPoint(new SARPoint(x, y, 0, 0, SARPoint.PointTypes.Clear)))
            {
                return _grid[x, y];
            }
            return null;
        }
        /// <summary>
        /// Adapter per GetPoint
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        IPoint IGrid.GetPoint(int x, int y)
        {
            return GetPoint(x, y);
        }

        public List<SARPoint> GetPossibleTargetPositions()
        {
            List<SARPoint> list = new List<SARPoint>();

            //scansiono la griglia base
            foreach (var p in _grid)
            {
                if (p.Type != SARPoint.PointTypes.Obstacle && p.Confidence > 0)
                {
                    list.Add(p);
                }
            }

            return list;
        }

        #region Generazione casuale
        public void RandomizeGrid(int seed, int shuffles)
        {
            Random rnd = new Random(seed);
            int iterCount = 0;
            var types = Enum.GetValues(typeof(SARPoint.PointTypes));

            while (iterCount < shuffles)
            {
                var point = _grid[rnd.Next(_numCol), rnd.Next(_numRow)];
                point.Type = (SARPoint.PointTypes)rnd.Next(types.Length);

                if (true)
                {
                    var p = BuildSARPoint(point.X, point.Y, rnd.NextDouble(), rnd.NextDouble(), point.Type);
                    _grid[point.X, point.Y] = p;
                }
                if (point.Type == SARPoint.PointTypes.Target)
                {
                    _estimatedTargetPositions.Add(point);
                }
                iterCount++;
            }

            //Debug
            var gridStr = new SARViewer().DisplayEnvironment(this);
            var gridConfStr = new SARViewer().DisplayProperty(this, SARViewer.SARPointAttributes.Confidence);

            _realTarget = RandomizeTargetPosition();
        }
        public void RandomizeGrid(int seed, int numTarget, float clearAreaRatio)
        {
            const float CONFIDENCE_SPREAD_FACTOR = 0.5F;
            Random randomizer = new Random(seed);
            int shufflesCount = 0;
            var cellTypes = Enum.GetValues(typeof(SARPoint.PointTypes));

            SARPoint[] targets = new SARPoint[numTarget];
            //seleziono le celle target   
            SARPoint _tmpTarget;
            for (int i = 0; i < numTarget; i++)
            {
                _tmpTarget = _grid[randomizer.Next(_numCol), randomizer.Next(_numRow)];
                _tmpTarget.Type = SARPoint.PointTypes.Target;
                targets[i] = _tmpTarget;
            }
            //PROBE
            //Debug.WriteLine(new SARViewer().DisplayEnvironment(this));

            //propago la confidence
            foreach (var t in targets)
            {
                var neighbors = GetNeighbors(t);
                //propagazione lineare valore di confidence
                foreach (var n in neighbors)
                {
                    var cell = n as SARPoint;
                    cell.Confidence = (int)(t.Confidence * CONFIDENCE_SPREAD_FACTOR);
                }
            }

            //seleziono le celle obstacle
            while (shufflesCount < (_numCol * _numRow) * (1 - clearAreaRatio))
            {
                var _tmpObstacle = _grid[randomizer.Next(_numCol), randomizer.Next(_numRow)];
                if (!targets.Contains(_tmpObstacle))
                {
                    shufflesCount++;
                    _tmpObstacle.Type = SARPoint.PointTypes.Obstacle;
                }
            }
            //PROBE
            //Debug.WriteLine(new SARViewer().DisplayEnvironment(this));
        } 

        /// <summary>
        /// Estrae casualmente un punto target tenendo conto del valore della prior (Confidence)
        /// </summary>
        /// <param name="targetNum"></param>
        /// <returns></returns>
        public SARPoint RandomizeTargetPosition(int targetNum = 1)
        {
            //accedo alla lista delle possibili posizioni candidate
            var maybeTargetPos = GetPossibleTargetPositions();
            if (maybeTargetPos.Count == 0)
            {
                var tgt = _grid[_numCol - 1, _numRow - 1];
                tgt.Type = SARPoint.PointTypes.Target;
                return tgt;
            }

            //calcolo Nmax
            int Nmax = (int) maybeTargetPos.Sum(x => { return (x.Confidence * 10); });

            //genero il pool per l'estrazione
            var extPool = new List<SARPoint>();
            foreach (var mT in maybeTargetPos)
            {      
                for (int i = 0; i < (mT.Confidence * 10); i++)
                {
                    extPool.Add(mT);
                }
            }
            //debug
            //extPool.ForEach(x => { Debug.WriteLine($"({x.X},{x.Y})"); });

            //estrattore
            var cryptoGen = RandomNumberGenerator.Create();
            byte[] secureSequence = new byte[16];
            cryptoGen.GetBytes(secureSequence);

            var rnd = new Random(BitConverter.ToUInt16(secureSequence, 0));

            //estraggo indice
            var index = rnd.Next(Nmax);

            //seleziono target
            var target = extPool[index];
            target.Type = SARPoint.PointTypes.Target;
            return target;
        }        
        #endregion

        #region IO
        private static SARGrid LoadFromFile(string path)
        {
            string gridFile = File.ReadAllText(path);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SARGrid>(gridFile);
        }

        /// <summary>
        /// Adapter per SARLib.SaveToFile
        /// </summary>
        /// <param name="destinationPath"></param>
        /// <returns></returns>
        public string SaveToFile(string destinationPath, string fileName = null)
        {
            return Toolbox.Saver.SaveToJsonFile(this, destinationPath, fileName);
        }
        #endregion
    }
}
