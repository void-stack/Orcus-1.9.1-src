﻿using System.Collections.Generic;
using ShellLibrary.Native;

namespace ShellLibrary
{
    internal class ShellPropertyDescriptionsCache
    {
        private static ShellPropertyDescriptionsCache cacheInstance;

        private IDictionary<PropertyKey, ShellPropertyDescription> propsDictionary;

        private ShellPropertyDescriptionsCache()
        {
            propsDictionary = new Dictionary<PropertyKey, ShellPropertyDescription>();
        }

        public static ShellPropertyDescriptionsCache Cache
        {
            get
            {
                if (cacheInstance == null)
                {
                    cacheInstance = new ShellPropertyDescriptionsCache();
                }
                return cacheInstance;
            }
        }

        public ShellPropertyDescription GetPropertyDescription(PropertyKey key)
        {
            if (!propsDictionary.ContainsKey(key))
            {
                propsDictionary.Add(key, new ShellPropertyDescription(key));
            }
            return propsDictionary[key];
        }
    }
}