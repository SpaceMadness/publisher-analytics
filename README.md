# Unity Asset Store Publisher Analytics
Google Analytics Wrapper for collecting anonymous statistics from the Unity Asset Store plugin users.

## Usage
* Register Google Analytics tracking ID: https://support.google.com/analytics/answer/1008080?hl=en
* Add [PublisherAnalytics.cs](https://github.com/SpaceMadness/publisher-analytics/blob/master/Assets/Editor/PublisherAnalytics.cs) to your plugin project. Make sure it's under `Editor` folder.
* Initialize `PublisherAnalytics` instance on editor launch:
  ```
  using UnityEditor;
  using SpaceMadness;

  [InitializeOnLoad]
  static class Autorun
  {
    static Autorun()
    {
      PublisherAnalytics.Initialize("UA-XXXXXXXXX-X", "1.0.0");
    }
  }
  ```
* Track events in your code:
  ```
  PublisherAnalytics.TrackEvent("My Category", "My Event");
  ```
  
  ## Custom Reports
  * Open Google Analytics Dashboard: https://analytics.google.com/
  * Create a new Custom Report:  
  ![1](https://user-images.githubusercontent.com/786644/31366455-c9604b88-ad25-11e7-8a6d-4fd8de3cf0a1.png)
  * Add `Sessions` metrics and `App Version` dimension:  
  ![2](https://user-images.githubusercontent.com/786644/31366457-cb0a6e1e-ad25-11e7-8d79-14282c1754c7.png)
  * Save the report
