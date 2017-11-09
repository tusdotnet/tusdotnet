using System.Collections.Generic;

namespace tusdotnet.Helpers
{
	/// <summary>
	/// Lock for a specific file so that it can only be updated by one caller.
	/// </summary>
	internal sealed class InMemoryFileLock
	{
		private readonly string _fileId;
		private static readonly HashSet<string> LockedFiles = new HashSet<string>();
		private bool _hasLock;

	    /// <summary>
	    /// Default constructor
	    /// </summary>
	    /// <param name="fileId">The file id to try to lock.</param>
	    public InMemoryFileLock(string fileId)
	    {
	        _fileId = fileId;
	        _hasLock = false;
	    }

	    /// <summary>
		/// Lock the file. Returns true if the file was locked or false if the file was already locked by another call.
		/// </summary>
		/// <returns>True if the file was locked or false if the file was already locked by another call.</returns>
		public bool Lock()
		{
			if (_hasLock)
			{
				return true;
			}

			lock (LockedFiles)
			{
				if (!LockedFiles.Contains(_fileId))
				{
					LockedFiles.Add(_fileId);
					_hasLock = true;
				}
				else
				{
					_hasLock = false;
				}
			}

			return _hasLock;
		}

		/// <summary>
		/// Release the lock if held. If not held by the caller, this method is a no op.
		/// </summary>
		public void ReleaseIfHeld()
		{
			if (!_hasLock)
			{
				return;
			}

			lock (LockedFiles)
			{
				LockedFiles.Remove(_fileId);
			}
		}
	}
}
