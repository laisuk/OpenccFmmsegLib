using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenccFmmsegLib
{
    /// <summary>Owns borrowed, pinned constructor buffers until the synchronous native call finishes.</summary>
    internal sealed class CustomDictionaryMarshaller : IDisposable
    {
        private readonly GCHandle[] _pins;
        private int _pinCount;

        internal IntPtr Specifications { get; private set; }
        internal UIntPtr Count { get; }

        internal CustomDictionaryMarshaller(IEnumerable<CustomDictSpec> specifications)
        {
            if (specifications == null) throw new ArgumentNullException(nameof(specifications));
            var snapshot = new List<CustomDictSpec>();
            var pinCapacity = 1;
            foreach (var spec in specifications)
            {
                if (spec == null)
                    throw new ArgumentException("Specifications cannot contain null.", nameof(specifications));
                pinCapacity = checked(pinCapacity + 1 + checked(spec.Entries.Count * 2));
                snapshot.Add(spec);
            }

            _pins = new GCHandle[pinCapacity];
            Count = new UIntPtr((uint)snapshot.Count);
            try
            {
                var nativeSpecs = new OpenccFmmsegNative.CustomDictionarySpecification[snapshot.Count];
                for (var i = 0; i < snapshot.Count; i++)
                {
                    var spec = snapshot[i];
                    var pairs = new OpenccFmmsegNative.CustomPair[spec.Entries.Count];
                    for (var j = 0; j < pairs.Length; j++)
                    {
                        pairs[j].Source = Pin(Utf8Z.FromString(spec.Entries[j].Key));
                        pairs[j].Target = Pin(Utf8Z.FromString(spec.Entries[j].Value));
                    }

                    nativeSpecs[i] = new OpenccFmmsegNative.CustomDictionarySpecification
                    {
                        Slot = (uint)spec.Slot,
                        Mode = (uint)spec.Mode,
                        Pairs = pairs.Length == 0 ? IntPtr.Zero : Pin(pairs),
                        PairCount = new UIntPtr((uint)pairs.Length)
                    };
                }

                // Count-based arrays have no sentinels. NULL/0 is the native empty-list contract.
                Specifications = nativeSpecs.Length == 0 ? IntPtr.Zero : Pin(nativeSpecs);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal IntPtr CreateInstance()
        {
            var instance = OpenccFmmsegNative.opencc_new_custom(Specifications, Count);
            if (instance != IntPtr.Zero) return instance;
            // Read the error on this thread before cleanup or any other native operation.
            var lastError = OpenccFmmseg.LastError();
            throw new InvalidOperationException($"Failed to initialize OpenCC with custom dictionaries. {lastError}".Trim());
        }

        private IntPtr Pin(Array buffer)
        {
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            _pins[_pinCount++] = handle;
            return handle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            while (_pinCount > 0)
                _pins[--_pinCount].Free();
            Specifications = IntPtr.Zero;
        }
    }
}
