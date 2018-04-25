using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocoptNet;

namespace AmalgamedDocoptExample
{
    internal class Program
    {
        private const string usage = @"Naval Fate.

    Usage:
      naval_fate.exe ship new <name>...
      naval_fate.exe ship <name> move <x> <y> [--speed=<kn>]
      naval_fate.exe ship shoot <x> <y>
      naval_fate.exe mine (set|remove) <x> <y> [--moored | --drifting]
      naval_fate.exe (-h | --help)
      naval_fate.exe --version

    Options:
      -h --help     
            Show this screen.
      --version     
            Show version.
      --speed=<kn>  
            Speed in knots [default: 10].
      --moored      
            Moored (anchored) mine.
      --drifting    
            Drifting mine.

    ";

        private class Options
        {
            public bool Ship { get; set; }
            public bool New { get; set; }
            public string[] Name { get; set; }
            public bool Move { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public double Speed { get; set; }
            public bool Shoot { get; set; }
            public bool Mine { get; set; }
            public bool Set { get; set; }
            public bool Remove { get; set; }
            public bool Moored { get; set; }
            public bool Drifting { get; set; }
        }

        private static void Main(string[] args)
        {
            try
            {
                var arguments = new Docopt().Apply(usage, args, version: "Naval Fate 2.0", exit: true);
                foreach (var argument in arguments)
                {
                    Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
                }

                var options = new Docopt().Bind<Options>(usage, args, version: "Naval Fate 2.0", exit: true);
                {
                    Console.WriteLine("Ship = {0}", options.Ship);
                    Console.WriteLine("New = {0}", options.New);
                    Console.WriteLine("Name = {0}", string.Join(",", options.Name));
                    Console.WriteLine("Move = {0}", options.Move);
                    Console.WriteLine("X = {0}", options.X);
                    Console.WriteLine("Y = {0}", options.Y);
                    Console.WriteLine("Speed = {0}", options.Speed);
                    Console.WriteLine("Shoot = {0}", options.Shoot);
                    Console.WriteLine("Mine = {0}", options.Mine);
                    Console.WriteLine("Set = {0}", options.Set);
                    Console.WriteLine("Remove = {0}", options.Remove);
                    Console.WriteLine("Moored = {0}", options.Moored);
                    Console.WriteLine("Drifting = {0}", options.Drifting);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }
}
