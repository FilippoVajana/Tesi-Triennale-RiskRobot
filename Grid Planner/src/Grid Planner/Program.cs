﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GridPlanner
{
    public class Program
    {        
        public static void Main(string[] args)
        {
            var grid = new SAREnvironmentLibrary.SARGrid(10, 20);            

            grid.RandomizeGrid(10, 5, .65F);
            Console.WriteLine(grid.ConvertToConsoleString());
            grid.SaveToFile(false);           

            Console.Write("\nPress Enter to exit");
            Console.ReadKey();            
        }
    }
}
