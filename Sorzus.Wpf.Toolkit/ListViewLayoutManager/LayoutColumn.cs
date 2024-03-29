﻿// -- FILE ------------------------------------------------------------------
// name       : LayoutColumn.cs
// created    : Jani Giannoudis - 2008.03.27
// language   : c#
// environment: .NET 3.0
// copyright  : (c) 2008-2012 by Itenso GmbH, Switzerland
// --------------------------------------------------------------------------

using System;
using System.Windows;
using System.Windows.Controls;

namespace Sorzus.Wpf.Toolkit.ListViewLayoutManager
{
    // ------------------------------------------------------------------------
    public abstract class LayoutColumn
    {
        // ----------------------------------------------------------------------
        protected static bool HasPropertyValue(GridViewColumn column, DependencyProperty dp)
        {
            if (column == null)
            {
                throw new ArgumentNullException("column");
            }
            object value = column.ReadLocalValue(dp);
            if (value != null && value.GetType() == dp.PropertyType)
            {
                return true;
            }

            return false;
        } // HasPropertyValue

        // ----------------------------------------------------------------------
        protected static double? GetColumnWidth(GridViewColumn column, DependencyProperty dp)
        {
            if (column == null)
            {
                throw new ArgumentNullException("column");
            }
            object value = column.ReadLocalValue(dp);
            if (value != null && value.GetType() == dp.PropertyType)
            {
                return (double) value;
            }

            return null;
        } // GetColumnWidth
    } // class LayoutColumn
} // namespace Itenso.Windows.Controls.ListViewLayout
// -- EOF -------------------------------------------------------------------