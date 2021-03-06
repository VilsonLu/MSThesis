﻿using SOMLibrary.Implementation.DistanceMeasure;
using SOMLibrary.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOMLibrary
{
    public class Node
    {
        public double[] Weights { get; set; }

        public Coordinate Coordinate { get; set; }

        private IDistanceMeasure _distanceMeasure { get; set; }

        public string Label { get; set; }

        public int ClusterGroup { get; set; }

        public int Count { get; set; }


        public Node(double[] weights, int x, int y)
        {
            Weights = weights;
            Coordinate = new Coordinate(x, y);
            Count = 0;

            _distanceMeasure = new EuclideanDistance();
        }

        /// <summary>
        /// Calculates the Euclidean distance between the weight vector and input vector
        /// </summary>
        /// <param name="inputVectors"></param>
        /// <returns>double - Euclidean distance</returns>
        public double GetDistance(double[] inputVectors)
        {
            return _distanceMeasure.GetDistance(Weights, inputVectors);
        }

        public double GetGridDistance(Node node)
        {
            double x = Math.Pow(Coordinate.X - node.Coordinate.X, 2);
            double y = Math.Pow(Coordinate.Y - node.Coordinate.Y, 2);
            return Math.Sqrt(x + y);
        }

        public void IncrementCount()
        {
            Count++;
        }

    }
}
