using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Api.Tests
{
    public class BasicTests: IClassFixture<WebApplicationFactory<Program>>
        {
            private readonly WebApplicationFactory<Program> _factory;

            public BasicTests(WebApplicationFactory<Program> factory)
            {
                _factory = factory;
            }
        }
    }

