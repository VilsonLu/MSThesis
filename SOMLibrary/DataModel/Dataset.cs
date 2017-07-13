﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SOMLibrary.Extensions;

namespace SOMLibrary.DataModel
{
    public class Dataset
    {
        public Feature[] Features { get; set; }

        public Instance[] Instances { get; set; }

        /// <summary>
        /// Set the feature to a label
        /// </summary>
        /// <param name="feature"></param>
        public void SetLabel(string feature)
        {
            var selectedFeature = Features.First(x => x.FeatureName == feature);

            if (selectedFeature == null)
            {
                throw new Exception("Feature does not exists");
            }

            selectedFeature.IsLabel = true;
        }

        public void SetKey(string feature)
        {
            var selectedFeature = Features.First(x => x.FeatureName == feature);

            if (selectedFeature == null)
            {
                throw new Exception("Feature does not exists");
            }

            selectedFeature.IsKey = true;
        }


        public T[] GetInstance<T>(int i)
        {
            int featureCounts = Features.Length;

            if (i > featureCounts)
            {
                throw new Exception("Selected feature does not exists");
            }

            var ignoredColumn = GetIgnoreColumns();
            List<T> values = new List<T>();
            var instance = Instances[i].Values;

            for (int j = 0; j < instance.Length; j++)
            {
                if (ignoredColumn.Any(x => x == j))
                {
                    continue;
                }

                values.Add(instance[j].ConvertType<T>());
            }

            return values.ToArray();
        }


        /// <summary>
        /// Get list of columns not to be used for training
        /// Example: Id, Labels
        /// </summary>
        /// <returns></returns>
        public List<int> GetIgnoreColumns()
        {
            var ignoreColumns = Features.Where(x => x.IsKey || x.IsLabel).Select(x => x.OrderNo);
            return ignoreColumns.ToList();
        }
    }
}
