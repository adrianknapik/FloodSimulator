document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("simulationForm");
  const resultContainer = document.querySelector(".result-container");
  const loading = document.querySelector(".loading");
  const errorMessage = document.getElementById("errorMessage");

  form.addEventListener("submit", async (e) => {
    e.preventDefault();

    const date = document.getElementById("date").value;
    const city = document.getElementById("city").value;
    const country = document.getElementById("country").value;

    // Show loading
    loading.style.display = "block";
    if (resultContainer) {
      resultContainer.style.display = "none";
    }
    errorMessage.style.display = "none";

    try {
      console.log("Sending request with:", { date, city, country });
      const response = await fetch(
        `http://localhost:5000/api/simulate?date=${date}&city=${city}&country=${country}`
      );
      if (!response.ok) {
        throw new Error("Simulation failed");
      }

      const responseData = await response.json();
      console.log("Received response:", responseData);

      if (!responseData || typeof responseData !== "object") {
        throw new Error("Invalid response format");
      }

      // Extract the simulation result from the tuple
      const result = responseData.item1;
      if (!result || typeof result !== "object") {
        throw new Error("Invalid simulation result format");
      }

      // Validate required data
      if (!result.riverLevels || !Array.isArray(result.riverLevels)) {
        throw new Error("Missing or invalid river levels data");
      }

      displayResults(result);
    } catch (error) {
      console.error("Error:", error);
      errorMessage.textContent = error.message;
      errorMessage.style.display = "block";
    } finally {
      loading.style.display = "none";
    }
  });
});

function displayResults(result) {
  try {
    console.log("Displaying results:", result);

    // Update summary cards
    document.getElementById("avgTemp").textContent = result.averageTemperature
      ? `${result.averageTemperature.toFixed(1)}°C`
      : "-";
    document.getElementById("soilMoisture").textContent =
      result.initialSoilMoisture
        ? `${result.initialSoilMoisture.toFixed(1)}%`
        : "-";
    document.getElementById("floodRisk").textContent = result.floodRisk || "-";

    // Generate dates array for 14 days
    const startDate = new Date(document.getElementById("date").value);
    const dates = Array.from({ length: 14 }, (_, i) => {
      const date = new Date(startDate);
      date.setDate(date.getDate() + i);
      return date.toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
      });
    });

    // Create river chart
    const riverData = [
      {
        x: dates,
        y: result.riverLevels || [],
        name: "River Level (m)",
        type: "scatter",
        line: { color: "blue" },
      },
      {
        x: dates,
        y: result.rainfall || [],
        name: "Rainfall (mm)",
        type: "scatter",
        yaxis: "y2",
        line: { color: "lightblue", dash: "dot" },
      },
      {
        x: dates,
        y: result.soilMoisture || [],
        name: "Soil Moisture (%)",
        type: "scatter",
        yaxis: "y2",
        line: { color: "green", dash: "dot" },
      },
    ];

    console.log("River chart data:", riverData);

    const riverLayout = {
      title: "River Level, Rainfall, and Soil Moisture Over Time",
      yaxis: { title: "River Level (m)" },
      yaxis2: {
        title: "Rainfall (mm) / Soil Moisture (%)",
        overlaying: "y",
        side: "right",
      },
      showlegend: true,
      xaxis: {
        title: "Date",
        tickangle: -45,
      },
    };

    Plotly.newPlot("riverChart", riverData, riverLayout);

    // Create risk gauge
    const percentage =
      result.currentLevel && result.maxCapacity
        ? (result.currentLevel / result.maxCapacity) * 100
        : 0;
    const color =
      percentage >= 100 ? "red" : percentage >= 80 ? "orange" : "green";

    const riskData = [
      {
        type: "indicator",
        mode: "gauge+number",
        value: percentage,
        title: { text: "Flood Risk Level" },
        gauge: {
          axis: { range: [0, 120] },
          bar: { color: color },
          steps: [
            { range: [0, 80], color: "green" },
            { range: [80, 100], color: "orange" },
            { range: [100, 120], color: "red" },
          ],
        },
      },
    ];

    console.log("Risk gauge data:", riskData);

    const riskLayout = {
      title: "Flood Risk Level",
      showlegend: false,
    };

    Plotly.newPlot("riskGauge", riskData, riskLayout);

    // Create temperature chart
    const tempData = [
      {
        x: dates,
        y: result.predictedTemps || [],
        name: "Predicted Temperature",
        type: "scatter",
        line: { color: "red" },
      },
      {
        x: dates,
        y: result.actualTemps || [],
        name: "Actual Temperature",
        type: "scatter",
        line: { color: "orange", dash: "dot" },
      },
    ];

    console.log("Temperature chart data:", tempData);

    const tempLayout = {
      title: "Temperature Comparison",
      yaxis: { title: "Temperature (°C)" },
      showlegend: true,
      xaxis: {
        title: "Date",
        tickangle: -45,
      },
    };

    Plotly.newPlot("tempChart", tempData, tempLayout);

    // Show results
    const resultContainer = document.querySelector(".result-container");
    if (resultContainer) {
      resultContainer.style.display = "block";
    }
  } catch (error) {
    console.error("Error displaying results:", error);
    errorMessage.textContent = "Error displaying results: " + error.message;
    errorMessage.style.display = "block";
  }
}
