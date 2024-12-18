// Copyright 2024-present Etherna SA
// This file is part of BeeTurbo.
// 
// BeeTurbo is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeeTurbo is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeeTurbo.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.MongODM.Core.Domain.Models;
using System;
using System.Collections.Generic;

namespace Etherna.BeeTurbo.Domain.Models
{
    public abstract class EntityModelBase : ModelBase, IEntityModel
    {
        private DateTime _creationDateTime;

        // Constructors and dispose.
        protected EntityModelBase()
        {
            _creationDateTime = DateTime.UtcNow;
        }

        public virtual void DisposeForDelete() { }

        // Properties.
        public virtual DateTime CreationDateTime { get => _creationDateTime; protected set => _creationDateTime = value; }
    }

    public abstract class EntityModelBase<TKey> : EntityModelBase, IEntityModel<TKey>
    {
        // Properties.
        public virtual TKey Id { get; protected set; } = default!;

        // Methods.
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            if (EqualityComparer<TKey>.Default.Equals(Id, default) ||
                obj is not IEntityModel<TKey> ||
                EqualityComparer<TKey>.Default.Equals((obj as IEntityModel<TKey>)!.Id, default)) return false;
            return GetType() == obj.GetType() &&
                   EqualityComparer<TKey>.Default.Equals(Id, (obj as IEntityModel<TKey>)!.Id);
        }

        public override int GetHashCode()
        {
            if (EqualityComparer<TKey>.Default.Equals(Id, default))
                return -1;
            return Id!.GetHashCode();
        }
    }
}