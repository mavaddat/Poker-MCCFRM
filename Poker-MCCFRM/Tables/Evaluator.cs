﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace SnapCall
{
    using Poker_MCCFRM;
    using System.Numerics;
    using System.Text.Json;

    public class Evaluator
	{
        private bool loaded = false;
		private HashMap handRankMap;
		private Dictionary<ulong, ulong> monteCarloMap = null;

        public Evaluator()
        {
            string fileName = "HandValueTable.txt";
            double loadFactor = 6.0;

            if (loaded) return;
            if (monteCarloMap != null)
                return;

            bool fiveCards = true;
            bool sixCards = true;
            bool sevenCards = true;

            DateTime start = DateTime.UtcNow;

            // Load hand rank table or create one if no file was given
            if (fileName != null && File.Exists(fileName))
            {
                if (File.Exists(fileName))
                {
                    Console.WriteLine("Loading table from {0}", fileName);
                    LoadFromFile(fileName);
                }
            }
            else
            {
                int minHashMapSize = (fiveCards ? 2598960 : 0) + (sixCards ? 20358520 : 0) + (sevenCards ? 133784560 : 0);
                handRankMap = new HashMap((uint)(minHashMapSize * loadFactor));
                if (fiveCards)
                {
                    GenerateFiveCardTable();
                }
                if (sixCards)
                {
                    Console.WriteLine("Generating new six card lookup table (20'358'520)");
                    GenerateSixCardTable();
                }
                if (sevenCards)
                {
                    Console.WriteLine("Generating new seven card lookup table (133'784'560)");
                    GenerateSevenCardTable();
                }

                //Console.WriteLine("Running monte carlo simulation");
                //GenerateMonteCarloMap(100000);
                Console.WriteLine("Writing table to disk");
                SaveToFile(fileName);
            }

            TimeSpan elapsed = DateTime.UtcNow - start;
            Console.WriteLine("Hand evaluator setup completed in {0}d {1}h {2}m {3}s", elapsed.Days, elapsed.Hours, elapsed.Minutes, elapsed.Seconds);
            loaded = true;
        }

		public int Evaluate(ulong bitmap)
		{
			// Check if 2-card monte carlo map has an evaluation for this hand
			//if (monteCarloMap.ContainsKey(bitmap)) return (int)monteCarloMap[bitmap];

			// Otherwise return the real evaluation
			return (int)handRankMap[bitmap];
		}

        public void SaveToFile(string fileName)
        {
            using var fileStream = File.Create(fileName);
            JsonSerializer.Serialize(fileStream, handRankMap);
        }

        private void LoadFromFile(string fileName)
        {
            using var fileStream = File.OpenRead(fileName);
            handRankMap = JsonSerializer.Deserialize<HashMap>(fileStream);
        }

		private void GenerateFiveCardTable()
		{
			var sourceSet = Enumerable.Range(0, 52).ToList();
			var combinations = new Combinatorics.Collections.Combinations<int>(sourceSet, 5);

			// Generate all possible 5 card hand bitmaps
            var handBitmaps = new List<ulong>();
            using (var progress = new ProgressBar())
            {
                long sharedLoopCounter = 0;

                foreach (List<int> values in combinations)
                {
                    handBitmaps.Add(values.Aggregate(0ul, (acc, el) => acc | (1ul << el)));
                    sharedLoopCounter++;
                    // Check if combinations.Count is larger than Double.MaxValue
                    if(combinations.Count > (BigInteger)Double.MaxValue)
                    {
                        throw new Exception("The number of combinations exceeds the maximum value for a double. Please use a different method to report progress.");
                    }
                    // Replace all instances of:
                    // progress.Report((double)sharedLoopCounter/ combinations.Count, sharedLoopCounter);
                    // with:
                    progress.Report((double)sharedLoopCounter / (double)combinations.Count, sharedLoopCounter);
                    progress.Report((double)sharedLoopCounter/ (double)combinations.Count, sharedLoopCounter);
                }
            }

			// Calculate hand strength of each hand
            var handStrengths = new Dictionary<ulong, HandStrength>();
            Console.WriteLine("Calculating hand strength (" + handBitmaps.Count + ")");

            using (var progress = new ProgressBar())
            {
                long sharedLoopCounter = 0;

                foreach (ulong bitmap in handBitmaps)
                {
                    var hand = new Hand(bitmap);
                    handStrengths.Add(bitmap, hand.GetStrength());
                    sharedLoopCounter++;
                    progress.Report((double)sharedLoopCounter / handBitmaps.Count, sharedLoopCounter);
                }
            }

			// Generate a list of all unique hand strengths
			var uniqueHandStrengths = new List<HandStrength>();
            Console.WriteLine("Generating equivalence classes");

            using (var progress = new ProgressBar())
            {
                long sharedLoopCounter = 0;

                foreach (KeyValuePair<ulong, HandStrength> strength in handStrengths)
                {
                    Utilities.BinaryInsert(uniqueHandStrengths, strength.Value);
                    sharedLoopCounter++;
                    progress.Report((double)sharedLoopCounter / handStrengths.Count, sharedLoopCounter);
                }
            }
			Console.WriteLine("{0} unique hand strengths", uniqueHandStrengths.Count);

            // Create a map of hand bitmaps to hand strength indices
            Console.WriteLine("Generating new five card lookup table (2'598'960)");
            using (var progress = new ProgressBar())
            {
                long sharedLoopCounter = 0;

                foreach (ulong bitmap in handBitmaps)
                {
                    var hand = new Hand(bitmap);
                    HandStrength strength = hand.GetStrength();
                    var equivalence = Utilities.BinarySearch(uniqueHandStrengths, strength);
                    if (equivalence == null) throw new Exception(string.Format("{0} hand not found", hand));
                    else
                    {
                        handRankMap[bitmap] = (ulong)equivalence;
                    }
                    sharedLoopCounter++;
                    progress.Report((double)sharedLoopCounter / handBitmaps.Count, sharedLoopCounter);
                }
			}
		}

		private void GenerateSixCardTable()
		{
            var sourceSet = Enumerable.Range(0, 52).ToList();
            var combinations = new Combinatorics.Collections.Combinations<int>(sourceSet, 6);
            using var progress = new ProgressBar();
            long sharedLoopCounter = 0;

            foreach (List<int> cards in combinations)
            {
                var subsets = new Combinatorics.Collections.Combinations<int>(cards, 5);
                var subsetValues = new List<ulong>();
                foreach (List<int> subset in subsets)
                {
                    ulong subsetBitmap = subset.Aggregate(0ul, (acc, el) => acc | (1ul << el));
                    subsetValues.Add(handRankMap[subsetBitmap]);
                }
                ulong bitmap = cards.Aggregate(0ul, (acc, el) => acc | (1ul << el));
                handRankMap[bitmap] = subsetValues.Max();

                sharedLoopCounter++;
                // Check if combinations.Count is larger than Double.MaxValue
                if (combinations.Count > (BigInteger)Double.MaxValue)
                {
                    throw new Exception("The number of combinations exceeds the maximum value for a double. Please use a different method to report progress.");
                }
                progress.Report((double)sharedLoopCounter / (double)combinations.Count, sharedLoopCounter);
            }
        }

		private void GenerateSevenCardTable()
		{
            var sourceSet = Enumerable.Range(0, 52).ToList();
            var combinations = new Combinatorics.Collections.Combinations<int>(sourceSet, 7);
            using var progress = new ProgressBar();
            long sharedLoopCounter = 0;

            foreach (List<int> cards in combinations)
            {
                var subsets = new Combinatorics.Collections.Combinations<int>(cards, 6);
                var subsetValues = new List<ulong>();
                foreach (List<int> subset in subsets)
                {
                    ulong subsetBitmap = subset.Aggregate(0ul, (acc, el) => acc | (1ul << el));
                    subsetValues.Add(handRankMap[subsetBitmap]);
                }
                ulong bitmap = cards.Aggregate(0ul, (acc, el) => acc | (1ul << el));
                handRankMap[bitmap] = subsetValues.Max();

                sharedLoopCounter++;
                // Check if combinations.Count is larger than Double.MaxValue
                if (combinations.Count > (BigInteger)Double.MaxValue)
                {
                    throw new Exception("The number of combinations exceeds the maximum value for a double. Please use a different method to report progress.");
                }
                progress.Report((double)sharedLoopCounter / (double)combinations.Count, sharedLoopCounter);
            }
        }

		private void GenerateMonteCarloMap(int iterations)
		{
			monteCarloMap = new Dictionary<ulong, ulong>();
            var sourceSet = Enumerable.Range(0, 52).ToList();
            var combinations = new Combinatorics.Collections.Combinations<int>(sourceSet, 2);
			int count = 0;
			foreach (List<int> cards in combinations)
			{
				Console.Write("{0}\r", count++);

				ulong bitmap = cards.Aggregate(0ul, (acc, el) => acc | (1ul << el));
				var hand = new Hand(bitmap);
				var deck = new Deck(removedCards: bitmap);

				ulong evaluationSum = 0;
				for (int i = 0; i < iterations; i++)
				{
					if (deck.CardsRemaining < 13) deck.Shuffle();
					evaluationSum += handRankMap[bitmap | deck.Draw(3)]; // TODO: this uses an old draw method
				}

				monteCarloMap[bitmap] = evaluationSum / (ulong)iterations;
			}

			foreach (KeyValuePair<ulong, ulong> kvp in monteCarloMap.OrderBy(kvp => kvp.Value))
			{
				var hand = new Hand(kvp.Key);
				hand.PrintColoredCards("\t");
				Console.WriteLine(kvp.Value);
				handRankMap[kvp.Key] = kvp.Value;
			}
			Console.ReadLine();
        }
	}
}
