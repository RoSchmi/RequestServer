#pragma once

#include <vector>
#include <chrono>
#include <thread>

#include <ArkeIndustries.CPPUtilities/Timer.h>

#include "Objects.h"
#include "CacheProvider.h"

namespace game_server {
	class updater {
		std::vector<objects::updatable*> objects;
		std::mutex lock;
		util::timer<> timer;
		word position ;
		word updates_per_tick;

		void tick();

		public:
			updater(word updates_per_tick, std::chrono::microseconds sleep_for);
			~updater();

			void add(objects::updatable* object);
			void remove(objects::updatable* object);
	};

	class cache_updater {
		cache_provider& cache;
		util::timer<> timer;
		word position;
		word updates_per_tick;

		void tick();

		public:
			cache_updater(cache_provider& cache, word updates_per_tick, std::chrono::microseconds sleep_for);
			~cache_updater();
	};
}