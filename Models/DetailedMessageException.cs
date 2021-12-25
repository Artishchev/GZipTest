using System;

namespace GZipTest.Models
{
    /// <summary>
    /// Custom exception for detailed error messages
    /// </summary>
    public class DetailedMessageException: Exception
    {
        /// <summary>
        /// Custom exception for detailed error messages
        /// </summary>
        /// <param name="message">Clear message for a user</param>
        /// <param name="inner">Original exception</param>
        public DetailedMessageException(string message, Exception inner)
        : base(message, inner)
        {
        }
    }
}
