using OnSale.Common.Entities;
using OnSale.Common.Responses;
using OnSale.Common.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xamarin.Essentials;

namespace OnSale.Prism.ViewModels
{
    public class ProductsPageViewModel : ViewModelBase
    {
        private readonly IApiService _apiService;
        private ObservableCollection<Products> _product;

        public ProductsPageViewModel(INavigationService navigationService, IApiService apiService)
            : base(navigationService)
        {
            _apiService = apiService;
            Title = "Products";
            LoadProductsAsync();
        }

        public ObservableCollection<Products> Products
        {
            get => _product;
            set => SetProperty(ref _product, value);
        }


        private async void LoadProductsAsync()
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                await App.Current.MainPage.DisplayAlert("Error", "Check the internet connection.", "Accept");
                return;
            }

            string url = App.Current.Resources["UrlAPI"].ToString();
            Response response = await _apiService.GetListAsync<Products>(url, "/api", "/Products");

            if (!response.IsSuccess)
            {
                await App.Current.MainPage.DisplayAlert("Error", response.Message, "Accept");
                return;
            }

            List<Products> myProducts = (List<Products>)response.Result;
            Products = new ObservableCollection<Products>(myProducts);
        }
    }
}
