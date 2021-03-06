﻿using GraphQL.Language.AST;
using System;
using System.Collections.Generic;

namespace Dapper.GraphQL
{
    /// <summary>
    /// A factory that creates Dapper entity mappers.
    /// </summary>
    public class EntityMapperFactory :
        IEntityMapperFactory
    {
        private readonly IServiceProvider serviceProvider;

        public EntityMapperFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Builds an entity mapper for the given entity type.
        /// </summary>
        /// <typeparam name="TEntityType">The type of entity to be mapped.</typeparam>
        /// <param name="resolve">A function that, given one or more entities, resolves to an entity instance to which child entities will be added.</param>
        /// <param name="selectionSet">The GraphQL selection set (optional).</param>
        /// <param name="splitOn">The types the query is split on.</param>
        /// <param name="shouldFilterDuplicates">True if duplicate objects should be filtered, false otherwise.</param>
        /// <returns>A Dapper mapping function.</returns>
        public Func<object[], TEntityType> Build<TEntityType>(
            Func<TEntityType, TEntityType, TEntityType> resolve = null,
            IHaveSelectionSet selectionSet = null,
            List<Type> splitOn = null,
            bool shouldFilterDuplicates = true)
            where TEntityType : class
        {
            if (resolve == null)
            {
                // A non-resolver, always resolves to the next object provided.
                resolve = (previous, current) => current;
            }

            // Build an entity mapper for the given type
            var mapper = serviceProvider.GetService(typeof(IEntityMapper<TEntityType>)) as IEntityMapper<TEntityType>;
            if (mapper == null)
            {
                throw new InvalidOperationException($"Could not find a mapper for type {typeof(IEntityMapper<TEntityType>).Name}");
            }

            TEntityType entity = null;

            // Setup the mapper to properly resolve its entity
            mapper.ResolveEntity = e => resolve(entity, e);

            return objs =>
            {
                // Map the object
                var next = mapper.Map(objs, selectionSet, splitOn);

                // Return null if we are returning a duplicate object
                if (shouldFilterDuplicates && object.ReferenceEquals(next, entity))
                {
                    return null;
                }

                // Save a reference to the entity
                entity = next;

                // And, return it
                return entity;
            };
        }

        /// <summary>
        /// Builds an entity mapper for the given entity type.
        /// </summary>
        /// <typeparam name="TEntityType">The type of entity to be mapped.</typeparam>
        /// <param name="resolve">A function that compares two values on the entity for equality, usually comparing primary keys.</param>
        /// <param name="selectionSet">The GraphQL selection set (optional).</param>
        /// <param name="splitOn">The types the query is split on.</param>
        /// <returns>A Dapper mapping function.</returns>
        public Func<object[], TEntityType> Build<TEntityType>(
            Func<TEntityType, object> selector,
            IHaveSelectionSet selectionSet = null,
            List<Type> splitOn = null)
            where TEntityType : class
        {
            var resolve = new Func<TEntityType, TEntityType, TEntityType>(
                (previous, current) =>
                {
                    TEntityType result = current;
                    if (previous != null)
                    {
                        // Compare against values on the objects (usually a primary key)
                        result = object.Equals(selector(previous), selector(current)) ? previous : current;
                    }
                    return result;
                }
            );

            // Build the mapper
            return Build(resolve, selectionSet, splitOn);
        }
    }
}