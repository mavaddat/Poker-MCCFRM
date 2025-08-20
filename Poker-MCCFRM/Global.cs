﻿using FASTER.core;
using SnapCall;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Poker_MCCFRM
{
    public static class Global
    {
        // adjust threads to cpu
        public const int NOF_THREADS = 12;

        // currently each round has the same raise values available
        // the values are multiples of the current pot
        // if new elements are added then the code must be adjusted in other places currently TODO
        public static List<float> raises = new List<float>() { 1f, 1.5f, 2.0f}; // fraction of current pot
        public const int buyIn = 200;
        public const int nofPlayers = 2; // !=2 not tested yet TODO
        public const int C = -100000;
        public const int regretFloor = -110000;

        public const int BB = 2;
        public const int SB = 1;

        // information abstraction parameters, currently this would be a
        // 169 - 200 - 200 - 200 abstraction, where the river is bucketed using OCHS and the turn and flop using EMD
        public const int nofRiverBuckets = 1000;
        public const int nofTurnBuckets = 1000;
        public const int nofFlopBuckets = 1000;
        // 100k or even 1 million shouldn't take too much time compared to the rest of the information abstraction
        public const int nofMCSimsPerPreflopHand = 500;
        // for the river, determines the river histogram size (in theory could be up to 169 but will be very slow) default 8
        public const int nofOpponentClusters = 16;
        public const int flopHistogramSize = 50;
        public const int turnHistogramSize = 50;

        // this is used to create the nofOpponentClusters, it can be increased (default 50)
        // with little time penalty because the clustering for 169 hands is very fast
        public const int preflopHistogramSize = 100;

        // dont change
        public static HandIndexer indexer_2;
        public static HandIndexer indexer_2_3;
        public static HandIndexer indexer_2_4;
        public static HandIndexer indexer_2_5;
        public static HandIndexer indexer_2_3_1;
        public static HandIndexer indexer_2_3_1_1;
        public static HandIndexer indexer_2_5_2;
        public static Evaluator handEvaluator;

        public static ConcurrentDictionary<string, Infoset> nodeMap = new ConcurrentDictionary<string, Infoset>(NOF_THREADS, 1000000);
        public static ConcurrentDictionary<string, Infoset> nodeMapBaseline = new ConcurrentDictionary<string, Infoset>(NOF_THREADS, 1000000);

        public static ThreadLocal<Deck> Deck = new ThreadLocal<Deck>(() => new Deck());
    }
}
