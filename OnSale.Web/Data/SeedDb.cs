﻿using Microsoft.EntityFrameworkCore;
using OnSale.Common.Entities;
using OnSale.Common.Enums;
using OnSale.Common.Models;
using OnSale.Common.Services;
using OnSale.Web.Data.Entities;
using OnSale.Web.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OnSale.Web.Data
{
    public class SeedDb
    {
        private readonly DataContext _context;
        private readonly IUserHelper _userHelper;
        private readonly IBlobHelper _blobHelper;
        private readonly IApiService _apiService;
        private readonly Random _random;

        public SeedDb(DataContext context, IUserHelper userHelper, IBlobHelper blobHelper, IApiService apiService)
        {
            _context = context;
            _userHelper = userHelper;
            _blobHelper = blobHelper;
            _apiService = apiService;
            _random = new Random();
        }

        public async Task SeedAsync()
        {
            await _context.Database.EnsureCreatedAsync();
            await CheckCountriesAsync();
            await CheckRolesAsync(); 
            await CheckUsersAsync();
            await CheckCategoriesAsync();
            await CheckProductsAsync();
        }

        private async Task CheckRolesAsync()
        {
            await _userHelper.CheckRoleAsync(UserType.Admin.ToString());
            await _userHelper.CheckRoleAsync(UserType.User.ToString());
        }

        private async Task CheckUsersAsync()
        {
            if (!_context.Users.Any())
            {
                await CheckAdminsAsync();
                await CheckBuyersAsync();
            }
        }

        private async Task CheckBuyersAsync()
        {
            for (int i = 1; i <= 100; i++)
            {
                await CheckUserAsync($"200{i}", $"buyer{i}@yopmail.com", UserType.User);
            }
        }

        private async Task CheckAdminsAsync()
        {
            await CheckUserAsync("1001", "admin1@yopmail.com", UserType.Admin);
        }

        private async Task<User> CheckUserAsync(string document, string email, UserType userType)
        {
            RandomUsers randomUsers;

            do
            {
                randomUsers = await _apiService.GetRandomUser("https://randomuser.me", "api");
            } while (randomUsers == null);

            Guid imageId = Guid.Empty;
            RandomUser randomUser = randomUsers.Results.FirstOrDefault();
            string imageUrl = randomUser.Picture.Large.ToString().Substring(22);
            Stream stream = await _apiService.GetPictureAsync("https://randomuser.me", imageUrl);
            if (stream != null)
            {
                imageId = await _blobHelper.UploadBlobAsync(stream, "users");
            }

            int cityId = _random.Next(1, _context.Cities.Count());
            User user = await _userHelper.GetUserAsync(email);
            if (user == null)
            {
                user = new User
                {
                    FirstName = randomUser.Name.First,
                    LastName = randomUser.Name.Last,
                    Email = email,
                    UserName = email,
                    PhoneNumber = randomUser.Cell,
                    Address = $"{randomUser.Location.Street.Number}, {randomUser.Location.Street.Name}",
                    Document = document,
                    UserType = userType,
                    City = await _context.Cities.FindAsync(cityId),
                    ImageId = imageId,
                    Latitude = double.Parse(randomUser.Location.Coordinates.Latitude),
                    Logitude = double.Parse(randomUser.Location.Coordinates.Longitude)
                };

                await _userHelper.AddUserAsync(user, "123456");
                await _userHelper.AddUserToRoleAsync(user, userType.ToString());
                string token = await _userHelper.GenerateEmailConfirmationTokenAsync(user);
                await _userHelper.ConfirmEmailAsync(user, token);
            }

            return user;
        }

        private async Task CheckProductsAsync()
        {
            if (!_context.Products.Any())
            {
                User user = await _userHelper.GetUserAsync("buyer1@yopmail.com");
                Category ropa = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Ropa");
                Category tecnologia = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Tecnologia");
                Category vehiculos = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Vehiculos");

                string lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Mauris gravida, nunc vel tristique cursus, velit nibh pulvinar enim, non pulvinar lorem leo eget felis. Proin suscipit dignissim nisl, at elementum justo laoreet sed. In tortor nibh, auctor quis est gravida, blandit elementum nulla. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Integer placerat nisi dui, id rutrum nisi viverra at. Interdum et malesuada fames ac ante ipsum primis in faucibus. Pellentesque sodales sollicitudin tempor. Fusce volutpat, purus sit amet placerat gravida, est magna gravida risus, a ultricies augue magna vel dolor. Fusce egestas venenatis velit, a ultrices purus aliquet sed. Morbi lacinia purus sit amet nisi vulputate mollis. Praesent in volutpat tortor. Etiam ac enim id ligula rutrum semper. Sed mattis erat sed condimentum congue. Vestibulum consequat tristique consectetur. Nunc in lorem in sapien vestibulum aliquet a vel leo.";

                await AddProductAsync(ropa, lorem, "Buzo GAP Hombre", 210000M, new string[] { "BuzoGAP1", "BuzoGAP2", "BuzoGAP3" }, user);
                await AddProductAsync(ropa, lorem, "Buzo Adidas Hombre", 270000M, new string[] { "BuzoAdidas1", "BuzoAdidas2", "BuzoAdidas3" }, user);
                
                await AddProductAsync(tecnologia, lorem, "iPhone 11", 3500000M, new string[] { "iPhone1", "iPhone2", "iPhone3" }, user);
                await AddProductAsync(tecnologia, lorem, "Redmi Note 9S", 1000000M, new string[] { "RedmiNote9S1", "RedmiNote9S2", "RedmiNote9S3" }, user);

                await AddProductAsync(vehiculos, lorem, "Pulsar 180 GT", 6600000M, new string[] { "Pulsar1801", "Pulsar1802", "Pulsar1803" }, user);
                await AddProductAsync(vehiculos, lorem, "Yamaha MT-03", 20000000M, new string[] { "YamahaMT031", "YamahaMT032", "YamahaMT033" }, user);

                await _context.SaveChangesAsync();
            }
        }

        private async Task AddProductAsync(Category category, string description, string name, decimal price, string[] images, User user)
        {
            Product product = new Product
            {
                Category = category,
                Description = description,
                IsActive = true,
                Name = name,
                Price = price,
                ProductImages = new List<ProductImage>(),
                Qualifications = GetRandomQualifications(description, user)
            };

            foreach (string image in images)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"wwwroot\\images", $"{image}.png");
                Guid imageId = await _blobHelper.UploadBlobAsync(path, "products");
                product.ProductImages.Add(new ProductImage { ImageId = imageId });
            }

            _context.Products.Add(product);
        }

        private ICollection<Qualification> GetRandomQualifications(string description, User user)
        {
            List<Qualification> qualifications = new List<Qualification>();
            for (int i = 0; i < 10; i++)
            {
                qualifications.Add(new Qualification
                {
                    Date = DateTime.UtcNow,
                    Remarks = description,
                    Score = _random.Next(1, 5),
                    User = user
                });
            }

            return qualifications;
        }

        private async Task CheckCategoriesAsync()
        {
            if (!_context.Categories.Any())
            {
                await AddCategoryAsync("Ropa");
                await AddCategoryAsync("Tecnologia");
                await AddCategoryAsync("Vehiculos");
                await _context.SaveChangesAsync();
            }
        }

        private async Task AddCategoryAsync(string name)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), $"wwwroot\\images", $"{name}.png");
            Guid imageId = await _blobHelper.UploadBlobAsync(path, "categories");
            _context.Categories.Add(new Category { Name = name, ImageId = imageId });
        }

        private async Task CheckCountriesAsync()
        {
            if (!_context.Countries.Any())
            {
                _context.Countries.Add(new Country
                {
                    Name = "Colombia",
                    Departments = new List<Department>
                {
                    new Department
                    {
                        Name = "Antioquia",
                        Cities = new List<City>
                        {
                            new City { Name = "Medellín" },
                            new City { Name = "Envigado" },
                            new City { Name = "Itagüí" }
                        }
                    },
                    new Department
                    {
                        Name = "Bogotá",
                        Cities = new List<City>
                        {
                            new City { Name = "Bogotá" }
                        }
                    },
                    new Department
                    {
                        Name = "Valle del Cauca",
                        Cities = new List<City>
                        {
                            new City { Name = "Calí" },
                            new City { Name = "Buenaventura" },
                            new City { Name = "Palmira" }
                        }
                    }
                }
                });
                _context.Countries.Add(new Country
                {
                    Name = "USA",
                    Departments = new List<Department>
                {
                    new Department
                    {
                        Name = "California",
                        Cities = new List<City>
                        {
                            new City { Name = "Los Angeles" },
                            new City { Name = "San Diego" },
                            new City { Name = "San Francisco" }
                        }
                    },
                    new Department
                    {
                        Name = "Illinois",
                        Cities = new List<City>
                        {
                            new City { Name = "Chicago" },
                            new City { Name = "Springfield" }
                        }
                    }
                }
                });
                await _context.SaveChangesAsync();
            }
        }
    }
}