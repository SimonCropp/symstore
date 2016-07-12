﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FileFormats
{
    /// <summary>
    /// A container that can provide ILayout instances for Types.
    /// </summary>
    public class LayoutManager
    {
        Dictionary<Type, ILayout> _layouts = new Dictionary<Type, ILayout>();
        List<Func<Type, LayoutManager, ILayout>> _layoutProviders = new List<Func<Type, LayoutManager, ILayout>>();
        Dictionary<Tuple<Type, uint>, ILayout> _arrayLayouts = new Dictionary<Tuple<Type, uint>, ILayout>();

        public LayoutManager() { }

        public void AddLayout(ILayout layout)
        {
            _layouts.Add(layout.Type, layout);
        }

        public void AddLayoutProvider(Func<Type, LayoutManager, ILayout> layoutProvider)
        {
            _layoutProviders.Add(layoutProvider);
        }

        public ILayout GetArrayLayout(Type arrayType, uint numElements)
        {
            if (!arrayType.IsArray)
            {
                throw new ArgumentException("The type parameter must be an array");
            }
            if (arrayType.GetArrayRank() != 1)
            {
                throw new ArgumentException("Multidemensional arrays are not supported");
            }

            ILayout layout;
            Tuple<Type, uint> key = new Tuple<Type, uint>(arrayType, numElements);
            if (!_arrayLayouts.TryGetValue(key, out layout))
            {
                Type elemType = arrayType.GetElementType();
                layout = new ArrayLayout(arrayType, GetLayout(elemType), numElements);
                _arrayLayouts.Add(key, layout);
            }
            return layout;
        }

        public ILayout GetArrayLayout<T>(uint numElements)
        {
            return GetArrayLayout(typeof(T), numElements);
        }

        public ILayout GetLayout<T>()
        {
            return GetLayout(typeof(T));
        }

        public ILayout GetLayout(Type t)
        {
            ILayout layout;
            if(!_layouts.TryGetValue(t, out layout))
            {
                foreach(Func<Type, LayoutManager, ILayout> provider in _layoutProviders)
                {
                    layout = provider(t, this);
                    if(layout != null)
                    {
                        break;
                    }
                }
                if(layout == null)
                {
                    throw new LayoutException("Unable to create layout for type " + t.FullName);
                }
                _layouts.Add(t, layout);
            }
            return layout;
        }
    }
}
