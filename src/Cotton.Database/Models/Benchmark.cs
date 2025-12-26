using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("benchmarks")]
    public class Benchmark : BaseEntity<Guid>
    {
        [Column("type")]
        public BenchmarkType Type { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("value")]
        public long Value { get; set; }

        [Column("units")]
        public string Units { get; set; } = null!;

        [Column("elapsed")]
        public TimeSpan Elapsed { get; set; }
    }
}
