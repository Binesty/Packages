﻿using Packages.Commands.Data;

namespace Packages.Commands
{
    public abstract class Context : IStorable
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Partition => Name;

        public StorableStatus StorableStatus { get; set; }

        public DateTime CreateDate => DateTime.UtcNow;
    }
}