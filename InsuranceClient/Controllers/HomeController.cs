using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InsuranceClient.Models;
using InsuranceClient.Models.ViewModel;
using InsuranceClient.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace InsuranceClient.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration configuration;
        public HomeController(IConfiguration config)
        {
            this.configuration = config;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(CustomerViewModel model)
        {
            if (ModelState.IsValid)
            {
                //step 1 : Upload/ Save customer image to Azure blob

                var customerId = Guid.NewGuid();
                StorageHelper storageHelper = new StorageHelper();
                storageHelper.ConnectionString = this.configuration.GetConnectionString("StorageConnection");

                //getting image path 
                var tempFile = Path.GetTempFileName();
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    await model.Image.CopyToAsync(fs);
                }

                var fileName = Path.GetFileName(model.Image.FileName);
                var tempPath = Path.GetDirectoryName(tempFile);
                var imagePath = Path.Combine(tempPath, string.Concat(customerId, "_", fileName));
                System.IO.File.Move(tempFile, imagePath);// rename temp file

                //Upload image
                var ImageUrl = await storageHelper.UploadCustomerImageAsync("images", imagePath);

                //Step 2: Save Customer data to Azure table

                Customer customer = new Customer(customerId.ToString(), model.InsuranceType);
                customer.FullName = model.FullName;
                customer.Email   = model.Email;
                customer.Amount = model.Amount;
                customer.AppDate = model.AppDate;
                customer.EndDate = model.EndDate;
                customer.Premium = model.Premium;
                customer.ImageURL = ImageUrl;
                await storageHelper.InsertCustomerAsync("Customers", customer);

                //Step 3: Add a confirmation message to Azure Queue

                await storageHelper.AddMessageAsync("insurance-requests", customer);
                return RedirectToAction("Index");
            }
            else
            {
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
