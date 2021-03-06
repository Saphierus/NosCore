﻿using System;
using Microsoft.EntityFrameworkCore;
using NosCore.Database;
using NosCore.Shared.I18N;

namespace NosCore.DAL
{
    public sealed class DataAccessHelper
    {
        private static DataAccessHelper instance;

        #region Members

        private DbContextOptions _option;

        #endregion

        private DataAccessHelper()
        {
        }

        public static DataAccessHelper Instance => instance ?? (instance = new DataAccessHelper());

        #region Methods

        /// <summary>
        ///     Creates new instance of database context.
        /// </summary>
        public NosCoreContext CreateContext()
        {
            return new NosCoreContext(_option);
        }
        public void InitializeForTest(DbContextOptions option)
        {
            _option = option;
        }

        public void Initialize(DbContextOptions option)
        {
            _option = option;
            using (var context = CreateContext())
            {
                try
                {
                    context.Database.Migrate();
                    context.Database.GetDbConnection().Open();
                    Logger.Log.Info(LogLanguage.Instance.GetMessageFromKey(LanguageKey.DATABASE_INITIALIZED));
                }
                catch (Exception ex)
                {
                    Logger.Log.Error("Database Error", ex);
                    Logger.Log.Error(LogLanguage.Instance.GetMessageFromKey(LanguageKey.DATABASE_NOT_UPTODATE));
                    throw;
                }
            }
        }

        #endregion
    }
}