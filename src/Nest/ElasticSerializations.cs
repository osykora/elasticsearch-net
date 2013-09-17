﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nest.Resolvers;
using Nest.Resolvers.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Nest
{
	public class ElasticSerializer
	{
		private readonly IConnectionSettings _settings;
		private readonly PropertyNameResolver _propertyNameResolver;
		private readonly List<JsonConverter> _extraConverters = new List<JsonConverter>();
		private readonly JsonSerializerSettings _serializationSettings;
		private readonly List<JsonConverter> _defaultConverters = new List<JsonConverter>
		{
			new IsoDateTimeConverter(),
			new FacetConverter()
		};

		public void ModifyJsonSerializationSettings(Action<JsonSerializerSettings> modifier)
		{
			modifier(this._serializationSettings);
		}

		public void AddConverter(JsonConverter converter)
		{
			this._serializationSettings.Converters.Add(converter);
			_extraConverters.Add(converter);
		}

		public ElasticSerializer(IConnectionSettings settings)
		{
			this._settings = settings;
			this._serializationSettings = this.CreateSettings();
			this._propertyNameResolver = new PropertyNameResolver();
		}

		/// <summary>
		/// Returns a response of type R based on the connection status without parsing status.Result into R
		/// </summary>
		/// <returns></returns>
		protected virtual R ToResponse<R>(ConnectionStatus status, bool allow404 = false) where R : BaseResponse
		{
			var isValid =
				(allow404)
				? (status.Error == null
					|| status.Error.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
				: (status.Error == null);
			var r = (R)Activator.CreateInstance(typeof(R));
			r.IsValid = isValid;
			r.ConnectionStatus = status;
			r.PropertyNameResolver = this._propertyNameResolver;
			return r;
		}
		/// <summary>
		/// Returns a response of type R based on the connection status by trying parsing status.Result into R
		/// </summary>
		/// <returns></returns>
		public virtual R ToParsedResponse<R>(ConnectionStatus status, bool allow404 = false, IEnumerable<JsonConverter> extraConverters = null) where R : BaseResponse
		{
			var isValid =
				(allow404)
				? (status.Error == null
					|| status.Error.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
				: (status.Error == null);
			if (!isValid)
				return this.ToResponse<R>(status, allow404);

			var r = this.Deserialize<R>(status.Result, extraConverters: extraConverters);
			r.IsValid = isValid;
			r.ConnectionStatus = status;
			r.PropertyNameResolver = this._propertyNameResolver;
			return r;
		}

		/// <summary>
		/// serialize an object using the internal registered converters without camelcasing properties as is done 
		/// while indexing objects
		/// </summary>
		public string Serialize(object @object, Formatting formatting = Formatting.Indented)
		{
			return JsonConvert.SerializeObject(@object, formatting, this._serializationSettings);
		}

		/// <summary>
		/// Deserialize an object 
		/// </summary>
		public T Deserialize<T>(string value, IEnumerable<JsonConverter> extraConverters = null)
		{

			var settings = this._serializationSettings;
			if (extraConverters.HasAny())
			{
				settings = this.CreateSettings();
				var concrete = extraConverters.OfType<ConcreteTypeConverter>().FirstOrDefault();
				if (concrete != null)
				{
					((ElasticResolver)settings.ContractResolver).ConcreteTypeConverter = concrete;
				}
				else
					settings.Converters = settings.Converters.Concat(extraConverters).ToList();

			}
			return JsonConvert.DeserializeObject<T>(value, settings);
		}
		private JsonSerializerSettings CreateSettings()
		{
			return new JsonSerializerSettings()
			{
				ContractResolver = new ElasticCamelCaseResolver(this._settings),
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Include,
				Converters = _defaultConverters.Concat(_extraConverters).ToList()
			};
		}
	}
}
