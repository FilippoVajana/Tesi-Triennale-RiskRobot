﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAREnvironment;
using System.IO;

namespace GridPlanner
{
    public class Program
    {        
        public static void Main(string[] args)
        {
            var grid = new SARGrid(10, 20);            

            grid.RandomizeGrid(10, 5, .65F);
            Console.WriteLine(grid.ConvertToConsoleString());

            var json = grid.SaveToFile(Directory.GetCurrentDirectory());
            Console.WriteLine($"Saved JSON At {json}");

            Console.Write("\nPress Enter to exit");
            Console.ReadKey();            
        }
    }
}
