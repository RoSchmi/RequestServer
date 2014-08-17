#pragma once

#include <vector>
#include <chrono>
#include <thread>

#include <Utilities/Timer.h>

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
			exported updater(word updates_per_tick, std::chrono::microseconds sleep_for);
			exported ~updater();

			exported void add(objects::updatable* object);
			exported void remove(objects::updatable* object);
	};

	class cache_updater {
		cache_provider& cache;
		util::timer<> timer;
		word position;
		word updates_per_tick;

		void tick();

		public:
			exported cache_updater(cache_provider& cache, word updates_per_tick, std::chrono::microseconds sleep_for);
			exported ~cache_updater();
	};
}