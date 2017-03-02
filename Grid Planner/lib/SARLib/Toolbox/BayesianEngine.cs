﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.IO;
using SARLib.SAREnvironment;

namespace SARLib.Toolbox
{
    public class BayesEngine
    {
        public class BayesFilter
        {
            Dictionary<int, double> _likelihood; //d,h -> p(d|h)
            Logger _logger;
            
            public BayesFilter(double errorRate, Logger logger)
            {                
                //Likelihood
                _likelihood = new Dictionary<int, double>()
                {
                    {1, 1 - errorRate }, //p(1|1)
                    {0, errorRate }, //p(1|0)           
                };

                _logger = logger;
            }

            public override string ToString()
            {
                return $"p(1|1)= {_likelihood[1]}; p(1|0)= {_likelihood[0]}";
            }

            private double Filter(int input, double prior) //prior = p(H=1)
            {
                //tabella della prior
                var pH = new Dictionary<int, double>()
                {
                    {1, prior },
                    {0, 1 - prior },
                };

                //calcolo p(D)
                double pD = 0;
                foreach (var e in _likelihood)
                {
                    pD += e.Value * pH[e.Key];
                }

                //calcolo posterior = p(1!D)
                double posterior = (_likelihood[input] * pH[input]) / pD;
                //logging
                _logger?.LogPosterior(input, prior, posterior, _likelihood[1]);

                return posterior;
            }

            /// <summary>
            /// Modifica il livello di Confidence applicando il Teorema di Bayes
            /// </summary>
            /// <param name="input"></param>
            /// <param name="prior"></param>
            /// <returns></returns>
            public double Filter(List<int> input, double prior)
            {
                double finalPosterior = 0;
                foreach (int data in input)
                {
                    //filtro dato in input
                    var post = Filter(data, prior);

                    //aggiorno la prior per il prossimo ciclo
                    prior = post;
                    
                    //salvo la posterior attuale
                    finalPosterior = post;                    
                }

                //logging
                _logger?.SaveFile();

                return finalPosterior;
            }

            /// <summary>
            /// Aggiorna la distribuzione di probabilità nell'ambiente di ricerca del parametro Confidence
            /// </summary>
            /// <param name="environment"></param>
            /// <param name="sensingPoint"></param>
            /// <returns></returns>
            public SARGrid UpdateConfidence(SARGrid environment, IPoint sensingPoint)
            {
                Func<double, double, double, string> PrintUpdateParameters = delegate (double pr, double d, double post)
                {
                    string result = string.Empty;
                    result = string.Format("UPDATING CONFIDENCE\n" +
                        "POINT: ({0},{1})\n" +
                        "PRIOR: {2:0.000}\n" +
                        "DELTA: {3:0.000}\n" +
                        "POSTERIOR: {4:0.000}\n", sensingPoint.X, sensingPoint.Y, pr, d, post);

                    return result;
                };

                ///1- lettura prior cella p(H)
                var prior = environment.GetPoint(sensingPoint.X, sensingPoint.Y).Confidence;

                ///2- lettura presenza target D (lista targets)
                var sensorRead = (environment._realTargets.Contains(sensingPoint)) ? 1 : 0; //OMG!! ;(

                ///3- calcolo posterior p(H|D) con Bayes
                var posterior = Filter(sensorRead, prior);
                environment.GetPoint(sensingPoint.X, sensingPoint.Y).Confidence = posterior;

                ///4- ottengo una copia della griglia ambiente
                SARPoint[,] envGrid = (SARPoint[,]) environment._grid.Clone();

                ///5- aggiornamento prior per i POI (?come?)
                var delta = posterior - prior; //valuto Δp nella posizione di rilevamento

                //DEBUG
                Debug.WriteLine(PrintUpdateParameters(prior, delta, posterior));

                foreach (var cell in envGrid)
                {
                    if (cell.Type != SARPoint.PointTypes.Obstacle)
                    {
                        //calcolo entità aggiornamento                    
                        double post = ComputePosteriorPropagation(cell, delta, environment.Distance(sensingPoint, cell));

                        //attuo l'aggiornamento della probabilità                    
                        environment.GetPoint(cell.X, cell.Y).Confidence = post; //provvisorio - portare a double/decimal 
                    }
                }
                
                return environment;
            }

            #region Formule propagazione aggiornamento posterior

            private Func<double, double, int, double> NegDeltaProp = delegate (double Pk, double dPn, int distance)
                {
                    var norm = Math.Abs(dPn / Math.Sqrt(distance));
                    return Pk + (1 - Pk) * norm;
                };

            private Func<double, double, int, double> PosDeltaProp = delegate (double Pk, double dPn, int distance)
            {
                var norm = Math.Abs(dPn / Math.Sqrt(distance));
                return Pk - Pk * norm;
                //return Pk - (1 - Pk) * (norm);
            };

            #endregion

            private double ComputePosteriorPropagation(SARPoint t, double delta, int distance)
            {
                //discrimino sul valore del delta
                if (delta >= 0)
                {
                    var d = PosDeltaProp(t.Confidence, delta, distance);
                    return d;
                }
                else
                {
                    var d = NegDeltaProp(t.Confidence, delta, distance);
                    return d;
                }
            }            
        }

        public class Logger
        {
            List<string> _logDiary;

            public Logger()
            {
                _logDiary = new List<string>();
            }
                        
            internal void LogPosterior(int input, double prior, double posterior, double errorRate)
            {
                string log = string.Empty;
                log = $"{errorRate};{input};{prior};{posterior}";
                _logDiary.Add(log);
            }

            internal void SaveFile()
            {
                //definizione file path
                var path = Path.GetFullPath(@"C:\Users\filip\Dropbox\Unimi\pianificazione\Grid Planner\lib\SARLib\Toolbox\Logs");
                path = Path.Combine(path, $"Bayeslog.txt");         

                //inserimento dati
                string log = string.Empty;
                foreach (var s in _logDiary)
                {
                    log += s + "\n";
                }

                File.WriteAllText(path, log);
            }
        }
    }

    
}