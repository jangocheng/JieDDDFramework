﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace JieDDDFramework.Data.EntityFramework.Migrate
{
    public static class MigrateDbContextExtensions
    {
        public static MigrateBuilder AddMigrateService(this IServiceCollection services)
        {
            return new MigrateBuilder(services);
        }

        public static IServiceProvider MigrateDbContext<TContext>(this IServiceProvider serviceProvider,
            Action<TContext, IServiceProvider> seeder = null) where TContext : Microsoft.EntityFrameworkCore.DbContext
        {

            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;

                var logger = services.GetRequiredService<ILogger<TContext>>();

                var context = services.GetService<TContext>();

                try
                {
                    logger.LogInformation($"DbContext=>{typeof(TContext).Name} 迁移开始");

                    var retry = Policy.Handle<SqlException>()
                        .WaitAndRetry(new TimeSpan[]
                        {
                            TimeSpan.FromSeconds(3),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(8),
                        });

                    retry.Execute(() =>
                    {
                        context.Database
                            .Migrate();
                        var dbContextSeed = services.GetService<IDbContextSeed<TContext>>();
                        dbContextSeed?.SeedAsync(context);
                        seeder?.Invoke(context, services);
                    });


                    logger.LogInformation($"DbContext=>{typeof(TContext).Name} 迁移完成");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"在DbContext=>{typeof(TContext).Name} 中迁移数据库出错");
                }
            }

            return serviceProvider;
        }
    }
}