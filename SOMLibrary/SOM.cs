﻿using ML.Common;
using Newtonsoft.Json;
using SOMLibrary.DataModel;
using SOMLibrary.Implementation.Builder;
using SOMLibrary.Implementation.LearningRate;
using SOMLibrary.Implementation.NeighborhoodRadius;
using SOMLibrary.Implementation.NodeLabeller;
using SOMLibrary.Interface;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SOMLibrary
{
    /// <summary>
    /// Self-Organizing Map
    /// </summary>
    public class SOM : Model
    {
        #region Properties
        public Guid MapId { get; set; }

        /// <summary>
        /// Learning Rate
        /// </summary>
        public double ConstantLearningRate { get; set; }

        public double FinalLearningRate { get; set; }

        public Node[,] Map { get; set; }

        /// <summary>
        /// Width of the map (X)
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the map (Y)
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Number of times it will train using the dataset
        /// </summary>
        public int Epoch { get; set; }

        public int TotalIteration { get; set; }

        public string FeatureLabel { get; set; }

        /// <summary>
        /// Number of neighbors for K-NN
        /// </summary>
        public int K { get; set; } = 3;

        public int GlobalEpoch { get; set; }

        public int TotalGlobalIteration { get; set; }

        public int LocalEpoch { get; set; }

        /// <summary>
        /// Initial neighborhood radius
        /// </summary>
        public double MapRadius { get; set; }

        public double FinalMapRadius { get; set; }

        #endregion

        #region Events
        public delegate void OnTrainingEventHandler(object sender, OnTrainingEventArgs args);
        public event OnTrainingEventHandler Training;
        #endregion

        #region Observations
        [JsonIgnore]
        public double LearningRateDisplay { get; set; }
        [JsonIgnore]
        public double RadiusDisplay { get; set; }
        #endregion

        #region Implementations

        /// <summary>
        /// To label the nodes
        /// </summary>
        private ILabel _labeller;

        /// <summary>
        /// To calculate the neighborhood radius
        /// </summary>
        private INeighborhoodRadius _neighborhoodRadius;
        public INeighborhoodRadius NeighborhoodRadiusCalculator
        {
            set
            {
                _neighborhoodRadius = value;
            }
        }

        /// <summary>
        /// To calculate the decay of the learning rate
        /// </summary>
        private ILearningRate _learningRate;
        public ILearningRate LearningRateCalculator
        {
            set
            {
                _learningRate = value;
            }
        }

        /// <summary>
        /// To calculate the neighborhood function
        /// </summary>
        private INeighborhoodKernel _neighborhoodKernel;
        public INeighborhoodKernel NeighborFunctionCalculator
        {
            set
            {
                _neighborhoodKernel = value;
            }
        }

        #endregion

        #region Constructor
        public SOM(SOMBuilder builder)
        {
            // Dimensions
            Width = builder.Width;
            Height = builder.Height;
            Map = new Node[Width, Height];

            // Learning Rate
            ConstantLearningRate = builder.InitialLearningRate;
            FinalLearningRate = builder.FinalLearningRate;

            // Neighborhood Radius
            MapRadius = builder.InitialRadius;
            FinalMapRadius = builder.FinalRadius;

            // Training Parameters
            Epoch = builder.Epoch;
            GlobalEpoch = builder.GlobalEpoch;
            K = builder.K;

            // Datasets
            FeatureLabel = builder.FeatureLabel;

            // Implementations
            NeighborhoodRadiusCalculator = builder.NeighborhoodRadiusCalculator;
            LearningRateCalculator = builder.LearningRateCalculator;
            NeighborFunctionCalculator = builder.NeighborhoodFunctionCalculator;
        }
        public SOM()
        {
            Width = 0;
            Height = 0;
            ConstantLearningRate = 0;
            Epoch = 1;
            Map = new Node[Width, Height];
            _learningRate = new PowerSeriesLearningRate(ConstantLearningRate);
            _neighborhoodRadius = new DecayNeighborhoodRadius(MapRadius);

        }

        public SOM(int x, int y)
        {
            Width = x;
            Height = y;
            ConstantLearningRate = 0.5;
            Epoch = 1;
            Map = new Node[x, y];
            _learningRate = new PowerSeriesLearningRate(ConstantLearningRate);
            _neighborhoodRadius = new DecayNeighborhoodRadius(MapRadius);
        }

        public SOM(int x, int y, double learningRate) : this(x, y)
        {
            ConstantLearningRate = learningRate;
            _learningRate = new PowerSeriesLearningRate(ConstantLearningRate);
            _neighborhoodRadius = new DecayNeighborhoodRadius(MapRadius);
            Epoch = 1;
        }

        public SOM(int x, int y, double learningRate, int epoch) : this(x, y, learningRate)
        {
            Epoch = epoch;
        }

        public SOM(int x, int y, double learningRate, int epoch, int k) : this(x, y, learningRate, epoch)
        {
            K = k;
        }

        public SOM(int x, int y, double learningRate, int epoch, int globalEpoch, int localEpoch, int k) : this(x, y, learningRate, epoch, k)
        {
            GlobalEpoch = globalEpoch;
            LocalEpoch = localEpoch;
        }

        #endregion

        /// <summary>
        /// Load the data from source
        /// </summary>
        /// <param name="reader"></param>
        public void GetData(IReader reader)
        {
            base.Dataset = reader.Read();
            TotalIteration = base.Dataset.Instances.Length * Epoch;
            TotalGlobalIteration = base.Dataset.Instances.Length * GlobalEpoch;
        }

        /// <summary>
        /// Initializes the SOM with random weights
        /// </summary>
        public void InitializeMap()
        {
            this.MapId = Guid.NewGuid();

            int weightCount = base.Dataset.WeightVectorCount;
            Random rand = new Random();

            for (int row = 0; row < Width; row++)
            {
                for (int col = 0; col < Height; col++)
                {
                    var vectors = new double[weightCount];
                    for (int count = 0; count < weightCount; count++)
                    {

                        vectors[count] = rand.NextDouble();
                    }

                    Node node = new Node(vectors, row, col);
                    Map[row, col] = node;
                }
            }
        }

        /// <summary>
        /// Train the SOM by adjusting the weights of the nodes.
        /// 
        /// Steps:
        /// 1. Initialize the SOM with random weights
        /// 2. Randomize the order of the instance
        /// 3. Get an instance from the dataset
        /// 4. Find the best matching unit. (Find the node with the least distance)
        /// 5. Update the neighborhood that are within the neighborhood radius
        /// 
        /// </summary>
        public override void Train()
        {
            // Initializes the nodes with random value
            InitializeMap();

            int instanceCount = base.Dataset.Instances.Length;
            var instances = base.Dataset.Instances;

            int t = 0; // iteration
            for (int i = 0; i < Epoch; i++)
            {
                // Randomize the order of the instance after every epoch
                base.Dataset.Shuffle();

                for (int d = 0; d < instanceCount; d++)
                {
                    // Get data from dataset
                    var instance = instances[d];

                    // Find the BMU (Best Matching Unit)
                    Node winningNode = FindBestMatchingUnit(instance);
                    winningNode.IncrementCount();
                    // Adjust the weights of the BMU and neighbor
                    UpdateNeighborhood(winningNode, instance, t);

                    if (Training != null)
                    {
                        Training(this, new OnTrainingEventArgs() { CurrentIteration = t, TotalIteration = TotalIteration });
                    }

                    t++;
                }
            }
        }

        #region SOM Functions

        /// <summary>
        /// Find the best matching unit based on the input data
        /// </summary>
        /// <param name="rowInstance"></param>
        /// <returns></returns>
        public virtual Node FindBestMatchingUnit(Instance instance)
        {
            double bestDistance = double.MaxValue;
            Node bestNode = null;

            for (int row = 0; row < Width; row++)
            {
                for (int col = 0; col < Height; col++)
                {
                    Node currentNode = Map[row, col];
                    double currentDistance = currentNode.GetDistance(instance.Values);

                    if (currentDistance < bestDistance)
                    {
                        bestDistance = currentDistance;
                        bestNode = currentNode;
                    }

                }
            }
            
    
            return bestNode;
        }

        /// <summary>
        /// Determines the neighborhood of the winning node
        /// </summary>
        /// <param name="winningNode"></param>
        /// <param name="rowInstance"></param>
        /// <param name="iteration"></param>
        protected virtual void UpdateNeighborhood(Node winningNode, Instance rowInstance, int iteration)
        {
            for (int row = 0; row < Width; row++)
            {
                for (int col = 0; col < Height; col++)
                {
                    var currentNode = Map[row, col];
                    var distanceToWinningNode = Math.Sqrt(winningNode.GetGridDistance(currentNode));
                    double learningRate = GetLearningRate(iteration);
                    double neighborhoodRadius = GetNeighborhoodRadius(iteration);

                    LearningRateDisplay = learningRate;
                    RadiusDisplay = neighborhoodRadius;
                    if (distanceToWinningNode <= neighborhoodRadius)
                    {
                        Map[row, col].Weights = AdjustWeights(currentNode, rowInstance.Values, learningRate);
                    }
                }
            }
        }

        /// <summary>
        /// Adjust the weights of the affected nodes
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="instance"></param>
        /// <param name="learningRate></param>
        /// <returns></returns>
        protected double[] AdjustWeights(Node currentNode, double[] instance, double learningRate)
        {
            var currentWeight = currentNode.Weights;

            for (int i = 0; i < currentWeight.Length; i++)
            {
                double newWeight = currentWeight[i] + (learningRate * (instance[i] - currentWeight[i]));
                currentWeight[i] = newWeight;
            }

            return currentWeight;
        }

        /// <summary>
        /// Calculate how fast model will learn every iteration
        /// </summary>
        /// <param name="iteration"></param>
        /// <returns></returns>
        protected double GetLearningRate(int iteration)
        {
            double learningRate = _learningRate.CalculateLearningRate(iteration, TotalGlobalIteration);
            return learningRate;
        }

        /// <summary>
        /// Neighborhood Radius: The neighborhood shrinks as time passes by
        /// </summary>
        /// <returns></returns>
        protected double GetNeighborhoodRadius(int iteration)
        {
            return _neighborhoodRadius.CalculateRadius(iteration, TotalGlobalIteration);
        }

        /// <summary>
        /// Neighborhood Function: Rate of change around the winning node
        /// </summary>
        /// <param name="distance"></param>
        /// <param name="neighborhoodRadius"></param>
        /// <returns></returns>
        protected double GetNeighborhoodFunction(double distance, double neighborhoodRadius)
        {
            return _neighborhoodKernel.CalculateNeighborhoodFunction(distance, neighborhoodRadius);
        }

        #endregion

        #region Others
        public virtual void AssignClusterLabel()
        {
            Hashtable clusterLabels = new Hashtable();
            Queue<string> letters = UtilityHelper.GetLetterQueue();

            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    int currentClusterGroup = Map[i, j].ClusterGroup;
                    if (clusterLabels.ContainsKey(currentClusterGroup))
                    {
                        Map[i, j].ClusterLabel = clusterLabels[currentClusterGroup].ToString();
                    }
                    else
                    {
                        string label = string.Empty;
                        if (letters.Count > 0)
                        {
                            label = letters.Dequeue();
                        }

                        clusterLabels.Add(currentClusterGroup, label);

                        Map[i, j].ClusterLabel = label;
                    }
                }
            }
        }

        /// <summary>
        /// Give label to each node in the map
        /// </summary>
        public void LabelNodes()
        {
            if (string.IsNullOrEmpty(this.FeatureLabel))
            {
                return;
            }

            _labeller = new KNNLabeller(base.Dataset, K, this.FeatureLabel);
            for (int row = 0; row < Width; row++)
            {
                for (int col = 0; col < Height; col++)
                {
                    Node node = Map[row, col];
                    Map[row, col].Label = _labeller.GetLabel(node);
                }
            }
        }
        #endregion

    }

    public class OnTrainingEventArgs : EventArgs
    {
        public int CurrentIteration { get; set; }
        public int TotalIteration { get; set; }
    }

}
