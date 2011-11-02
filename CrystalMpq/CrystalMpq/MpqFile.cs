﻿#region Copyright Notice
// This file is part of CrystalMPQ.
// 
// Copyright (C) 2007-2011 Fabien BARBIER
// 
// CrystalMPQ is licenced under the Microsoft Reciprocal License.
// You should find the licence included with the source of the program,
// or at this URL: http://www.microsoft.com/opensource/licenses.mspx#Ms-RL
#endregion

using System;
using System.IO;
using System.Threading;

namespace CrystalMpq
{
	/// <summary>This class represents a file stored in an <see cref="MpqArchive"/>.</summary>
	public sealed class MpqFile
	{
		private MpqArchive owner;
		private MpqHashTable.HashEntry hashEntry;
		private string name;
		private long offset;
		private uint compressedSize;
		private uint uncompressedSize;
		private MpqFileFlags flags;
		private uint seed;
		private int index;
		private bool listed;

		internal MpqFile(MpqArchive owner, int index, long offset, uint compressedSize, uint uncompressedSize, uint flags)
		{
			if (owner == null) throw new ArgumentNullException("owner");
			this.owner = owner;
			this.index = index;
			this.offset = offset;
			this.compressedSize = compressedSize;
			this.uncompressedSize = uncompressedSize;
			this.flags = unchecked((MpqFileFlags)flags);
			this.name = "";
			this.seed = 0;
			this.listed = false;
		}

		internal void BindHashTableEntry(MpqHashTable.HashEntry hashEntry) { this.hashEntry = hashEntry; }

		/// <summary>Called internally when the name has been detected.</summary>
		/// <param name="name">Detected filename.</param>
		/// <param name="cache">If set to <c>true</c>, remember the filename.</param>
		/// <param name="listed">If set to <c>true</c>, the name was detected from the listfile.</param>
		/// <remarks>Right now, the method will only update the seed when needed.</remarks>
		internal void OnNameDetected(string name, bool cache = false, bool listed = false)
		{
			if (!string.IsNullOrEmpty(this.name)) return;

			// TODO: Improve the name caching mechanism (Global hash table for MPQ archives ?)
			if (cache || (flags & MpqFileFlags.Encrypted) != 0)
				this.seed = ComputeSeed(name);
			if (cache || IsPatch) this.name = name; // Always cache the filename if the file is a patch… This is needed for base file lookup.
			if (cache) this.listed = listed;
		}

		private static uint ComputeSeed(string filename)
		{
			// Calculate the seed based on the file name and not the full path.
			// I really don't know why but it worked with the full path for a lot of files…
			// But now it's fixed at least
			int index = filename.LastIndexOf('\\');
			return Encryption.Hash(index >= 0 ? filename.Substring(index + 1) : filename, 0x300);
		}

		/// <summary>Gets the archive to whom this file belongs.</summary>
		public MpqArchive Archive { get { return owner; } }

		/// <summary>Gets the name for this file, or null if the filename is not known.</summary>
		public string Name { get { return name; } }

		/// <summary>Gets the offset of this file in the archive.</summary>
		public long Offset { get { return offset; } }

		/// <summary>Gets the size of this file.</summary>
		public long Size { get { return uncompressedSize; } }

		/// <summary>Gets the compressed size of this file.</summary>
		/// <remarks>If the file is not compressed, CompressedSize will return the same value than Size.</remarks>
		public long CompressedSize {get { return compressedSize; } }

		/// <summary>Gets the flags that apply to this file.</summary>
		public MpqFileFlags Flags { get { return flags; } }

		/// <summary>Gets a value indicating whether this file is encrypted.</summary>
		/// <value><c>true</c> if this file is encrypted; otherwise, <c>false</c>.</value>
		public bool IsEncrypted { get { return (flags & MpqFileFlags.Encrypted) != 0; } }

		/// <summary>Gets a value indicating whether this file is compressed.</summary>
		/// <value><c>true</c> if this file is compressed; otherwise, <c>false</c>.</value>
		public bool IsCompressed { get { return (flags & MpqFileFlags.Compressed) != 0; } }

		/// <summary>Gets a value indicating whether this file is a patch.</summary>
		/// <value><c>true</c> if this file is a patch; otherwise, <c>false</c>.</value>
		public bool IsPatch { get { return (flags & MpqFileFlags.Patch) != 0; } }

		/// <summary>Gets the LCID associated with this file.</summary>
		public int Lcid { get { return hashEntry.Locale; } }

		/// <summary>Gets the index of the file in the collection.</summary>
		/// <remarks>In the current impelmentation, this index is also the index of the file in the archive's block table.</remarks>
		public int Index { get { return index; } }

		/// <summary>Gets the seed associated with this file.</summary>
		/// <remarks>The seed is a value that is used internally to decrypt some files.</remarks>
		/// <value>The seed associated with this file.</value>
		internal uint Seed { get { return seed; } }

		/// <summary>Gets a value indicating whether the file was found in the list file of the archive.</summary>
		/// <remarks>This can only be true if the list file was parsed.</remarks>
		/// <value><c>true</c> if this instance is listed; otherwise, <c>false</c>.</value>
		public bool IsListed { get { return listed; } }

		/// <summary>Opens the file for reading.</summary>
		/// <returns>Returns a Stream object which can be used to read data in the file.</returns>
		/// <remarks>Files can only be opened once, so don't forget to close the stream after you've used it.</remarks>
		public MpqFileStream Open() { return new MpqFileStream(this); }

		/// <summary>Opens a patched file for reading.</summary>
		/// <param name="baseStream">A base stream.</param>
		/// <returns>Returns a Stream object which can be used to read data in the file.</returns>
		/// <remarks>
		/// This method should only be used for explicitly providing a base stream when the <see cref="MpqFile"/> is a patch.
		/// Files can only be opened once, so don't forget to close the stream after you've used it.
		/// </remarks>
		/// <exception cref="InvalidOperationException">This instance of <see cref="MpqFile"/> is not a patch.</exception>
		/// <exception cref="ArgumentNullException"><paramref name="baseStream"/> is <c>null</c>.</exception>
		public MpqFileStream Open(Stream baseStream)
		{
			if (!IsPatch) throw new InvalidOperationException();

			if (baseStream == null) throw new ArgumentNullException("baseStream");

			return new MpqFileStream(this);
		}
	}
}