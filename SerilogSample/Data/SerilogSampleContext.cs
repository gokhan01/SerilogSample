using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SerilogSample.Models;

namespace SerilogSample.Data
{
    public class SerilogSampleContext : DbContext
    {
        public SerilogSampleContext (DbContextOptions<SerilogSampleContext> options)
            : base(options)
        {
        }

        public DbSet<SerilogSample.Models.Product> Product { get; set; } = default!;
    }
}
