﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NMockaroo.Exceptions;

namespace NMockaroo
{
    /// <summary>
    ///     A client for the Mockaroo API.  Read more at https://www.mockaroo.com/api/docs
    /// </summary>
    public class MockarooClient
    {
        private const string MockarooApiUrl = @"http://www.mockaroo.com/api/generate.json?key={0}&count={1}";
        private readonly string _apiKey;

        public MockarooClient(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException("apiKey",
                    "API Key required. Please make sure to specify your API key in your configuration file.");
            }
            _apiKey = apiKey;
        }

        public IEnumerable<T> GetData<T>(int count)
        {
            IEnumerable<T> data;
            var request = CreateRequest<T>(count);

            using (var client = new HttpClient())
            {
                var response = client.SendAsync(request).Result;
                var responseContent = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new MockarooException(responseContent);
                }

                data = JsonConvert.DeserializeObject<IEnumerable<T>>(responseContent);
            }

            return data;
        }

        private HttpRequestMessage CreateRequest<T>(int count)
        {
            var fieldMetadata = GetFields<T>().ToArray();

            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var jsonString = JsonConvert.SerializeObject(
                fieldMetadata,
                Formatting.None,
                jsonSettings);

            var url = string.Format(MockarooApiUrl, _apiKey, count);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url),
                Content = new StringContent(jsonString)
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return request;
        }

        private IEnumerable<Dictionary<string, object>> GetFields<T>()
        {
            var fields = typeof (T).GetProperties();
            return fields.Select(GetFieldMetadata);
        }

        private Dictionary<string, object> GetFieldMetadata(PropertyInfo field)
        {
            Dictionary<string, object> fieldData = null;

            if (field.CustomAttributes.Any())
            {
                var customAttributeData = field.GetCustomAttributesData().ToArray();

                if (customAttributeData.Any())
                {
                    foreach (
                        var data in
                            customAttributeData.Where(data => data.NamedArguments != null && data.NamedArguments.Any()))
                    {
                        if (data.NamedArguments != null)
                        {
                            foreach (var arg in data.NamedArguments)
                            {
                                if (fieldData == null)
                                {
                                    fieldData = new Dictionary<string, object>();
                                }

                                fieldData.Add(arg.MemberInfo.Name, GetValueOrArray(arg.TypedValue));
                            }
                        }
                    }
                }
            }

            return fieldData;
        }

        private static object GetValueOrArray(CustomAttributeTypedArgument argument)
        {
            if (argument.Value.GetType() == typeof (ReadOnlyCollection<CustomAttributeTypedArgument>))
            {
                return (
                    from cataElement in (ReadOnlyCollection<CustomAttributeTypedArgument>) argument.Value
                    select cataElement.Value.ToString()
                    ).ToArray();
            }

            return argument.Value;
        }
    }
}