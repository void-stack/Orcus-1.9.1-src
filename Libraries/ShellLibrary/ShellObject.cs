﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using ShellLibrary.Native;

namespace ShellLibrary
{
    /// <summary>
    ///     The base class for all Shell objects in Shell Namespace.
    /// </summary>
    abstract public class ShellObject : IDisposable, IEquatable<ShellObject>
    {
        private static MD5CryptoServiceProvider hashProvider = new MD5CryptoServiceProvider();

        /// <summary>
        ///     A friendly name for this object that' suitable for display
        /// </summary>
        private string _internalName;

        /// <summary>
        ///     Parsing name for this Object e.g. c:\Windows\file.txt,
        ///     or ::{Some Guid}
        /// </summary>
        private string _internalParsingName;

        /// <summary>
        ///     PID List (PIDL) for this object
        /// </summary>
        private IntPtr _internalPIDL = IntPtr.Zero;

        private int? hashValue;

        /// <summary>
        ///     Internal member to keep track of the native IShellItem2
        /// </summary>
        internal IShellItem2 nativeShellItem;

        private ShellObject parentShellObject;

        private ShellProperties properties;

        internal ShellObject()
        {
        }

        internal ShellObject(IShellItem2 shellItem)
        {
            nativeShellItem = shellItem;
        }

        /// <summary>
        ///     Indicates whether this feature is supported on the current platform.
        /// </summary>
        public static bool IsPlatformSupported
        {
            get
            {
                // We need Windows Vista onwards ...
                return Environment.OSVersion.Version.Major >= 6;
            }
        }

        /// <summary>
        ///     Return the native ShellFolder object as newer IShellItem2
        /// </summary>
        /// <exception cref="System.Runtime.InteropServices.ExternalException">
        ///     If the native object cannot be created.
        ///     The ErrorCode member will contain the external error code.
        /// </exception>
        virtual internal IShellItem2 NativeShellItem2
        {
            get
            {
                if (nativeShellItem == null && ParsingName != null)
                {
                    Guid guid = new Guid(ShellIIDGuid.IShellItem2);
                    int retCode = ShellNativeMethods.SHCreateItemFromParsingName(ParsingName, IntPtr.Zero, ref guid,
                        out nativeShellItem);

                    if (nativeShellItem == null || !CoreErrorHelper.Succeeded(retCode))
                    {
                        throw new Exception("Shell item could not be created.",
                            Marshal.GetExceptionForHR(retCode));
                    }
                }
                return nativeShellItem;
            }
        }

        /// <summary>
        ///     Return the native ShellFolder object
        /// </summary>
        virtual internal IShellItem NativeShellItem
        {
            get { return NativeShellItem2; }
        }

        /// <summary>
        ///     Gets access to the native IPropertyStore (if one is already
        ///     created for this item and still valid. This is usually done by the
        ///     ShellPropertyWriter class. The reference will be set to null
        ///     when the writer has been closed/commited).
        /// </summary>
        internal IPropertyStore NativePropertyStore { get; set; }

        /// <summary>
        ///     Gets an object that allows the manipulation of ShellProperties for this shell item.
        /// </summary>
        public ShellProperties Properties
        {
            get
            {
                if (properties == null)
                {
                    properties = new ShellProperties(this);
                }
                return properties;
            }
        }

        /// <summary>
        ///     Gets the parsing name for this ShellItem.
        /// </summary>
        virtual public string ParsingName
        {
            get
            {
                if (_internalParsingName == null && nativeShellItem != null)
                {
                    _internalParsingName = ShellHelper.GetParsingName(nativeShellItem);
                }
                return _internalParsingName ?? string.Empty;
            }
            protected set { _internalParsingName = value; }
        }

        /// <summary>
        ///     Gets the normal display for this ShellItem.
        /// </summary>
        virtual public string Name
        {
            get
            {
                if (_internalName == null && NativeShellItem != null)
                {
                    IntPtr pszString = IntPtr.Zero;
                    HResult hr = NativeShellItem.GetDisplayName(ShellNativeMethods.ShellItemDesignNameOptions.Normal,
                        out pszString);
                    if (hr == HResult.Ok && pszString != IntPtr.Zero)
                    {
                        _internalName = Marshal.PtrToStringAuto(pszString);

                        // Free the string
                        Marshal.FreeCoTaskMem(pszString);
                    }
                }
                return _internalName;
            }

            protected set { _internalName = value; }
        }

        /// <summary>
        ///     Gets the PID List (PIDL) for this ShellItem.
        /// </summary>
        internal virtual IntPtr PIDL
        {
            get
            {
                // Get teh PIDL for the ShellItem
                if (_internalPIDL == IntPtr.Zero && NativeShellItem != null)
                {
                    _internalPIDL = ShellHelper.PidlFromShellItem(NativeShellItem);
                }

                return _internalPIDL;
            }
            set { _internalPIDL = value; }
        }

        /// <summary>
        ///     Gets a value that determines if this ShellObject is a link or shortcut.
        /// </summary>
        public bool IsLink
        {
            get
            {
                try
                {
                    ShellNativeMethods.ShellFileGetAttributesOptions sfgao;
                    NativeShellItem.GetAttributes(ShellNativeMethods.ShellFileGetAttributesOptions.Link, out sfgao);
                    return (sfgao & ShellNativeMethods.ShellFileGetAttributesOptions.Link) != 0;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (NullReferenceException)
                {
                    // NativeShellItem is null
                    return false;
                }
            }
        }

        /// <summary>
        ///     Gets a value that determines if this ShellObject is a file system object.
        /// </summary>
        public bool IsFileSystemObject
        {
            get
            {
                try
                {
                    ShellNativeMethods.ShellFileGetAttributesOptions sfgao;
                    NativeShellItem.GetAttributes(ShellNativeMethods.ShellFileGetAttributesOptions.FileSystem, out sfgao);
                    return (sfgao & ShellNativeMethods.ShellFileGetAttributesOptions.FileSystem) != 0;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (NullReferenceException)
                {
                    // NativeShellItem is null
                    return false;
                }
            }
        }

        /// <summary>
        ///     Gets the parent ShellObject.
        ///     Returns null if the object has no parent, i.e. if this object is the Desktop folder.
        /// </summary>
        public ShellObject Parent
        {
            get
            {
                if (parentShellObject == null && NativeShellItem2 != null)
                {
                    IShellItem parentShellItem;
                    HResult hr = NativeShellItem2.GetParent(out parentShellItem);

                    if (hr == HResult.Ok && parentShellItem != null)
                    {
                        parentShellObject = ShellObjectFactory.Create(parentShellItem);
                    }
                    else if (hr == HResult.NoObject)
                    {
                        // Should return null if the parent is desktop
                        return null;
                    }
                    else
                    {
                        throw new Exception(hr.ToString());
                    }
                }

                return parentShellObject;
            }
        }

        /// <summary>
        ///     Release the native objects.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Determines if two ShellObjects are identical.
        /// </summary>
        /// <param name="other">The ShellObject to comare this one to.</param>
        /// <returns>True if the ShellObjects are equal, false otherwise.</returns>
        public bool Equals(ShellObject other)
        {
            bool areEqual = false;

            if (other != null)
            {
                IShellItem ifirst = NativeShellItem;
                IShellItem isecond = other.NativeShellItem;
                if (ifirst != null && isecond != null)
                {
                    int result = 0;
                    HResult hr = ifirst.Compare(
                        isecond, SICHINTF.SICHINT_ALLFIELDS, out result);

                    areEqual = (hr == HResult.Ok) && (result == 0);
                }
            }

            return areEqual;
        }

        /// <summary>
        ///     Creates a ShellObject subclass given a parsing name.
        ///     For file system items, this method will only accept absolute paths.
        /// </summary>
        /// <param name="parsingName">The parsing name of the object.</param>
        /// <returns>A newly constructed ShellObject object.</returns>
        public static ShellObject FromParsingName(string parsingName)
        {
            return ShellObjectFactory.Create(parsingName);
        }

        /// <summary>
        ///     Updates the native shell item that maps to this shell object. This is necessary when the shell item
        ///     changes after the shell object has been created. Without this method call, the retrieval of properties will
        ///     return stale data.
        /// </summary>
        /// <param name="bindContext">Bind context object</param>
        public void Update(IBindCtx bindContext)
        {
            HResult hr = HResult.Ok;

            if (NativeShellItem2 != null)
            {
                hr = NativeShellItem2.Update(bindContext);
            }

            if (CoreErrorHelper.Failed(hr))
            {
                throw new Exception(hr.ToString());
            }
        }

        /// <summary>
        ///     Overrides object.ToString()
        /// </summary>
        /// <returns>A string representation of the object.</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        ///     Release the native and managed objects
        /// </summary>
        /// <param name="disposing">Indicates that this is being called from Dispose(), rather than the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _internalName = null;
                _internalParsingName = null;
                properties = null;
                parentShellObject = null;
            }

            if (properties != null)
            {
                properties.Dispose();
            }

            if (_internalPIDL != IntPtr.Zero)
            {
                ShellNativeMethods.ILFree(_internalPIDL);
                _internalPIDL = IntPtr.Zero;
            }

            if (nativeShellItem != null)
            {
                Marshal.ReleaseComObject(nativeShellItem);
                nativeShellItem = null;
            }

            if (NativePropertyStore != null)
            {
                Marshal.ReleaseComObject(NativePropertyStore);
                NativePropertyStore = null;
            }
        }

        /// <summary>
        ///     Implement the finalizer.
        /// </summary>
        ~ShellObject()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Returns the hash code of the object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (!hashValue.HasValue)
            {
                uint size = ShellNativeMethods.ILGetSize(PIDL);
                if (size != 0)
                {
                    byte[] pidlData = new byte[size];
                    Marshal.Copy(PIDL, pidlData, 0, (int) size);
                    byte[] hashData = hashProvider.ComputeHash(pidlData);
                    hashValue = BitConverter.ToInt32(hashData, 0);
                }
                else
                {
                    hashValue = 0;
                }
            }
            return hashValue.Value;
        }

        /// <summary>
        ///     Returns whether this object is equal to another.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>Equality result.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as ShellObject);
        }

        /// <summary>
        ///     Implements the == (equality) operator.
        /// </summary>
        /// <param name="leftShellObject">First object to compare.</param>
        /// <param name="rightShellObject">Second object to compare.</param>
        /// <returns>True if leftShellObject equals rightShellObject; false otherwise.</returns>
        public static bool operator ==(ShellObject leftShellObject, ShellObject rightShellObject)
        {
            if ((object) leftShellObject == null)
            {
                return ((object) rightShellObject == null);
            }
            return leftShellObject.Equals(rightShellObject);
        }

        /// <summary>
        ///     Implements the != (inequality) operator.
        /// </summary>
        /// <param name="leftShellObject">First object to compare.</param>
        /// <param name="rightShellObject">Second object to compare.</param>
        /// <returns>True if leftShellObject does not equal leftShellObject; false otherwise.</returns>
        public static bool operator !=(ShellObject leftShellObject, ShellObject rightShellObject)
        {
            return !(leftShellObject == rightShellObject);
        }
    }
}