﻿#region USING_DIRECTIVES
using Npgsql;

using System;
#endregion

namespace TheGodfather.Exceptions
{
    public class DatabaseOperationException : NpgsqlException
    {
        public DatabaseOperationException()
        {

        }

        public DatabaseOperationException(string message) 
            : base(message)
        {

        }

        public DatabaseOperationException(Exception inner) 
            : base("Database operation failed!", inner)
        {

        }

        public DatabaseOperationException(string message, Exception inner) 
            : base(message, inner)
        {

        }
    }
}
